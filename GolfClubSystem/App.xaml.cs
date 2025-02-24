using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Markup;
using GolfClubSystem.Data;
using GolfClubSystem.Models;
using GolfClubSystem.Services;
using GolfClubSystem.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serilog;

namespace GolfClubSystem;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private CancellationTokenSource _cts;
    public TelegramService _telegramService;
    public IConfigurationRoot _configuration;

    protected override void OnStartup(StartupEventArgs e)
    {
        _configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory()) // Set the base directory to current directory
            .AddJsonFile("appsettings.json") // Read the connection string from appsettings.json
            .Build();

        base.OnStartup(e);

        CultureInfo culture = new CultureInfo("ru-RU");
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;

        FrameworkElement.LanguageProperty.OverrideMetadata(
            typeof(FrameworkElement),
            new FrameworkPropertyMetadata(XmlLanguage.GetLanguage(culture.IetfLanguageTag))
        );

        // Инициализация логгера
        Logger.Initialize();

        // Глобальная обработка ошибок
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        DispatcherUnhandledException += App_DispatcherUnhandledException;

        // Init telegram
        var token = _configuration.GetSection("Token").Value;
        InitializeBot(token);
        StartBackgroundTask();

        // Show login window
        var loginWindow = new LoginWindow();
        loginWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("Application exiting.");
        Logger.Shutdown();

        base.OnExit(e);
    }

    #region Private methods

    private void StartBackgroundTask()
    {
        _cts = new CancellationTokenSource();
        Task.Run(() => BackgroundTask(_cts.Token), _cts.Token);
        // Task.Run(() => BackgroundNotifyTask(_cts.Token), _cts.Token);
    }

    private readonly Dictionary<int, int> _jobExecutionCount = new(); // Хранит JobId -> Количество выполнений

    private async Task BackgroundNotifyTask(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                using var unitOfWork = new UnitOfWork();
                var currentDate = DateTime.Now;
                var dayName = currentDate.ToString("dddd", new CultureInfo("ru-RU")).ToLower();
                var now = TimeOnly.FromDateTime(currentDate);

                // Fetch jobs scheduled for the current day (дневные и ночные)
                var jobs = await unitOfWork.NotifyJobRepository.GetAll()
                    .Include(j => j.Shift)
                    .ThenInclude(s => s.Scheduledays)
                    .Where(n => n.Shift.Scheduledays.Any(sd => sd.DayOfWeek.ToLower() == dayName))
                    .ToListAsync(token);

                foreach (var job in jobs)
                {
                    var shiftDay = job.Shift.Scheduledays.FirstOrDefault(sd => sd.DayOfWeek.ToLower() == dayName);
                    if (shiftDay == null) continue;

                    // Проверяем количество выполнений за день из временного хранилища
                    int executionCountToday = _jobExecutionCount.TryGetValue(job.Id, out int count) ? count : 0;

                    if (executionCountToday >= 3)
                    {
                        continue; // Пропускаем задание, если оно уже выполнено 3 раза
                    }

                    int remainingExecutions = 3 - executionCountToday;

                    // Определяем рабочий диапазон времени
                    TimeSpan startTimeSpan, endTimeSpan;
                    DateTime baseDate = currentDate.Date; // Базовая дата для текущего дня

                    if (shiftDay.WorkStart > shiftDay.WorkEnd)
                    {
                        // Ночное расписание (например, 22:00 текущего дня до 6:00 следующего дня)
                        var nightStart = shiftDay.WorkStart.Value; // Предположим, 22:00
                        var nightEnd = shiftDay.WorkEnd.Value; // Предположим, 6:00 следующего дня

                        startTimeSpan = nightStart.ToTimeSpan();
                        endTimeSpan = nightEnd.ToTimeSpan();

                        // Если текущее время больше или равно началу ночной смены, но меньше 6:00 следующего дня
                        if (now >= nightStart)
                        {
                            // Используем следующий день для окончания
                            endTimeSpan = endTimeSpan.Add(TimeSpan.FromDays(1));
                        }
                        else
                        {
                            // Если ещё не началась ночь, используем текущий день для начала
                            startTimeSpan = startTimeSpan.Add(TimeSpan.FromDays(-1));
                        }
                    }
                    else
                    {
                        // Дневное расписание (между WorkStart и WorkEnd текущего дня)
                        startTimeSpan = shiftDay.WorkStart.Value.ToTimeSpan();
                        endTimeSpan = shiftDay.WorkEnd.Value.ToTimeSpan();

                        // Проверяем, не закончилось ли рабочее время
                        if (now >= shiftDay.WorkEnd.Value)
                        {
                            continue; // Рабочее время закончилось
                        }
                    }

                    var currentTimeSpan = now.ToTimeSpan();

                    // Убедимся, что работа может быть выполнена в оставшееся время
                    if (currentTimeSpan >= endTimeSpan)
                    {
                        continue; // Рабочее время закончилось
                    }

                    // Генерируем уникальные случайные времена в оставшемся рабочем диапазоне
                    var randomTimes = GenerateUniqueRandomTimes(
                        currentTimeSpan > startTimeSpan ? currentTimeSpan : startTimeSpan,
                        endTimeSpan,
                        remainingExecutions);

                    foreach (var randomTime in randomTimes)
                    {
                        TimeSpan delay = randomTime > currentTimeSpan
                            ? randomTime - currentTimeSpan
                            : TimeSpan.Zero;

                        if (delay <= TimeSpan.Zero)
                        {
                            continue; // Пропускаем, если время уже прошло
                        }

                        await Task.Delay(delay, token);

                        // Выполняем задание
                        List<Worker> selectedWorkers;
                        if (job.Percentage.HasValue)
                        {
                            var totalCountQuery = unitOfWork.WorkerRepository.GetAll()
                                .Where(w => w.ChatId != null);

                            if (job.OrganizationId.HasValue)
                            {
                                totalCountQuery =
                                    totalCountQuery.Where(w => w.OrganizationId == job.OrganizationId.Value);
                            }

                            if (job.ZoneId.HasValue)
                            {
                                totalCountQuery = totalCountQuery.Where(w => w.ZoneId == job.ZoneId.Value);
                            }

                            var totalCount = await totalCountQuery.CountAsync(token);
                            var countToFetch = (int)Math.Ceiling(totalCount * (job.Percentage.Value / 100m));

                            selectedWorkers = await totalCountQuery
                                .OrderBy(w => Guid.NewGuid()) // Randomize selection
                                .Take(countToFetch) // Limit to the count
                                .ToListAsync(token);
                        }
                        else
                        {
                            var workerIds = JsonSerializer.Deserialize<List<int>>(job.WorkerIds);
                            selectedWorkers = await unitOfWork.WorkerRepository.GetAll()
                                .Where(w => workerIds.Contains(w.Id))
                                .ToListAsync(token);
                        }

                        if (selectedWorkers.Any())
                        {
                            List<NotifyHistory> notifyHistory = [];
                            List<NotifyHistory> notifyHistoryExist = [];
                            foreach (var worker in selectedWorkers)
                            {
                                await _telegramService.SendMessageByUsernameAsync(worker.Id, job.Message);

                                var existNotifyHistory = await unitOfWork.NotifyHistoryRepository.GetAll(true)
                                    .FirstOrDefaultAsync(
                                        h => h.ArrivalTime.Date == currentDate.Date && h.WorkerId == worker.Id, token);

                                if (existNotifyHistory != null)
                                {
                                    existNotifyHistory.Status = 2;
                                    existNotifyHistory.ArrivalTime = DateTime.Now;
                                    notifyHistoryExist.Add(existNotifyHistory);
                                }
                                else
                                {
                                    notifyHistory.Add(new NotifyHistory
                                    {
                                        ArrivalTime = DateTime.Now,
                                        WorkerId = worker.Id,
                                        Status = 2
                                    });
                                }
                            }

                            if (notifyHistory.Any())
                            {
                                await unitOfWork.NotifyHistoryRepository.AddRangeAsync(notifyHistory);
                            }

                            if (notifyHistoryExist.Any())
                            {
                                await unitOfWork.NotifyHistoryRepository.UpdateRangeAsync(notifyHistoryExist);
                            }
                        }

                        // Увеличиваем счётчик выполнений для этого job
                        _jobExecutionCount[job.Id] = executionCountToday + 1;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in background notification task");
            }

            // Очищаем счётчик в полночь или добавляем логику очистки
            if (DateTime.Now.Hour == 0 && DateTime.Now.Minute == 0)
            {
                _jobExecutionCount.Clear();
            }

            // Задержка перед следующей итерацией цикла (например, 1 минута)
            await Task.Delay(TimeSpan.FromMinutes(1), token);
        }
    }

// Вспомогательный метод для генерации уникальных случайных времен
    private List<TimeSpan> GenerateUniqueRandomTimes(TimeSpan start, TimeSpan end, int count)
    {
        var random = new Random();
        var times = new HashSet<TimeSpan>();
        int startMinutes = (int)start.TotalMinutes;
        int endMinutes = (int)end.TotalMinutes;

        while (times.Count < Math.Min(count, endMinutes - startMinutes)) // Ограничиваем по возможному диапазону
        {
            int randomMinutes = random.Next(startMinutes, endMinutes);
            times.Add(TimeSpan.FromMinutes(randomMinutes));
        }

        return times.OrderBy(t => t).ToList();
    }

    private async Task BackgroundTask(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var options = new ParallelOptions
                {
                    CancellationToken = token,
                    MaxDegreeOfParallelism = Environment.ProcessorCount
                };

                using var unitOfWork = new UnitOfWork();
                var zones = unitOfWork.ZoneRepository.GetAll(true)
                    .Where(z => z.DeletedAt == null)
                    .ToList();

                await Parallel.ForEachAsync(zones, options, async (zone, ct) =>
                {
                    try
                    {
                        using var zoneUnitOfWork = new UnitOfWork();
                        using var terminalService = new TerminalService(zone.Login, zone.Password);

                        await ProcessZone(zone, zoneUnitOfWork, terminalService, false);
                        await ProcessZone(zone, zoneUnitOfWork, terminalService, true);
                        await ProcessNotifyZone(zone.NotifyIp, unitOfWork, terminalService);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, $"Error processing zone {zone.Id}: {ex.Message}");
                    }
                });

                await Task.Delay(TimeSpan.FromSeconds(60), token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unhandled domain exception occurred.");
                Console.WriteLine($"Ошибка: {ex.Message}");
            }
        }
    }

    private async Task ProcessZone(Zone zone, UnitOfWork unitOfWork, TerminalService terminalService, bool isExitIp)
    {
        var nowDate = DateTime.Now;
        var ip = isExitIp ? zone.ExitIp : zone.EnterIp;

        var histories = await terminalService.GetFilteredUserHistoriesAsync(ip, nowDate.ToString("yyyy-MM-dd"));
        if (histories.Count == 0) return;

        // Группируем события по сотрудникам
        var groupedHistories = histories
            .Where(h => !string.IsNullOrEmpty(h.employeeNoString) && !string.IsNullOrEmpty(h.time))
            .GroupBy(h => int.Parse(h.employeeNoString))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(h => DateTimeOffset.Parse(h.time).DateTime).ToList());

        // Получаем данные сотрудников из базы
        var workerIds = groupedHistories.Keys.ToList();
        var workers = await unitOfWork.WorkerRepository.GetAll()
            .AsNoTracking()
            .Include(w => w.Schedule)
            .ThenInclude(sc => sc.Scheduledays)
            .Include(w => w.Schedule)
            .ThenInclude(sc => sc.Holidays)
            .Where(w => w.DeletedAt == null && workerIds.Contains(w.Id))
            .ToListAsync();

        var existingHistories = await unitOfWork.HistoryRepository.GetAll()
            .Where(h => h.ArrivalTime.Date == nowDate.Date && workerIds.Contains(h.WorkerId))
            .ToListAsync();

        var newHistories = new List<Employeehistory>();
        var updatedHistories = new List<Employeehistory>();

        foreach (var worker in workers)
        {
            if (!groupedHistories.TryGetValue(worker.Id, out var workerHistories)) continue;

            var latestHistory = workerHistories.First();
            var terminalHistoryDateTime = DateTimeOffset.Parse(latestHistory.time).DateTime;
            var terminalHistoryTime = TimeOnly.FromDateTime(terminalHistoryDateTime);

            // Проверяем, есть ли запись в базе
            var employeeHistory = existingHistories.FirstOrDefault(h => h.WorkerId == worker.Id);

            var schedule = worker.Schedule;
            var dayName = nowDate.ToString("dddd", new CultureInfo("ru-RU")).ToLower();
            var workingDay =
                schedule?.Scheduledays.FirstOrDefault(s => s.DayOfWeek.ToLower() == dayName && s.WorkStart != null);

            if (workingDay == null) continue;
            if (schedule?.Holidays.Any(h => h.HolidayDate.Date == terminalHistoryDateTime.Date) ?? false) continue;

            if (employeeHistory != null)
            {
                // Обновляем существующую запись
                if (employeeHistory.MarkTime >= terminalHistoryDateTime) continue;

                if (isExitIp)
                {
                    if (employeeHistory.LeaveTime == terminalHistoryDateTime) continue;

                    if (employeeHistory.Status is 2 or 3)
                    {
                        employeeHistory.LeaveTime = terminalHistoryDateTime;
                    }
                    else if (schedule.PermissibleEarlyLeaveStart > terminalHistoryTime)
                    {
                        employeeHistory.LeaveTime = terminalHistoryDateTime;
                        employeeHistory.Status = 4;
                    }
                    else
                    {
                        employeeHistory.LeaveTime = terminalHistoryDateTime;
                    }
                }
                else
                {
                    if (employeeHistory.Status == 4)
                    {
                        var arriveTime = TimeOnly.FromDateTime(employeeHistory.ArrivalTime);
                        if (arriveTime < schedule.PermissibleLateTimeEnd)
                        {
                            employeeHistory.Status = 1;
                        }
                    }
                }

                employeeHistory.MarkTime = terminalHistoryDateTime;
                employeeHistory.MarkZoneId = zone.Id;
                updatedHistories.Add(employeeHistory);
            }
            else
            {
                // Создаем новую запись
                var newEmployeeHistory = new Employeehistory
                {
                    ArrivalTime = terminalHistoryDateTime,
                    WorkerId = worker.Id,
                    MarkZoneId = zone.Id,
                    MarkTime = terminalHistoryDateTime
                };

                if (workingDay.WorkStart >= terminalHistoryTime)
                {
                    newEmployeeHistory.Status = 1;
                }
                else if (schedule.PermissibleLateTimeStart <= terminalHistoryTime &&
                         schedule.PermissibleLateTimeEnd >= terminalHistoryTime)
                {
                    newEmployeeHistory.Status = 1;
                }
                else if (terminalHistoryTime > schedule.PermissibleLateTimeEnd &&
                         terminalHistoryTime < schedule.PermissionToLateTime)
                {
                    newEmployeeHistory.Status = 3;
                }
                else if (terminalHistoryTime > schedule.PermissionToLateTime)
                {
                    newEmployeeHistory.Status = 2;
                }

                newHistories.Add(newEmployeeHistory);
            }
        }

        if (updatedHistories.Any())
        {
            await unitOfWork.HistoryRepository.UpdateRangeAsync(updatedHistories);
        }

        if (newHistories.Any())
        {
            await unitOfWork.HistoryRepository.AddRangeAsync(newHistories);
        }
    }

    private async Task ProcessNotifyZone(string ipAddress, UnitOfWork unitOfWork, TerminalService terminalService)
    {
        var nowDate = DateTime.Now;
        var ip = ipAddress;

        var histories = await terminalService.GetFilteredUserHistoriesAsync(ip, nowDate.ToString("yyyy-MM-dd"));
        if (histories.Count == 0) return;

        // Группируем события по сотрудникам
        var groupedHistories = histories
            .Where(h => !string.IsNullOrEmpty(h.employeeNoString) && !string.IsNullOrEmpty(h.time))
            .GroupBy(h => int.Parse(h.employeeNoString))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(h => DateTimeOffset.Parse(h.time).DateTime).ToList());

        // Получаем данные сотрудников из базы
        var workerIds = groupedHistories.Keys.ToList();
        var workers = await unitOfWork.WorkerRepository.GetAll()
            .AsNoTracking()
            .Include(w => w.Schedule)
            .ThenInclude(sc => sc.Scheduledays)
            .Include(w => w.Schedule)
            .ThenInclude(sc => sc.Holidays)
            .Where(w => w.DeletedAt == null && workerIds.Contains(w.Id))
            .ToListAsync();

        var existingHistories = await unitOfWork.NotifyHistoryRepository.GetAll()
            .Where(h => h.ArrivalTime.Date == nowDate.Date && workerIds.Contains(h.WorkerId))
            .ToListAsync();

        var updatedHistories = new List<NotifyHistory>();

        foreach (var worker in workers)
        {
            if (!groupedHistories.TryGetValue(worker.Id, out var workerHistories)) continue;

            var latestHistory = workerHistories.First();
            var terminalHistoryDateTime = DateTimeOffset.Parse(latestHistory.time).DateTime;

            // Проверяем, есть ли запись в базе
            var employeeHistory = existingHistories.FirstOrDefault(h => h.WorkerId == worker.Id);

            var schedule = worker.Schedule;
            var dayName = nowDate.ToString("dddd", new CultureInfo("ru-RU")).ToLower();
            var workingDay =
                schedule?.Scheduledays.FirstOrDefault(s => s.DayOfWeek.ToLower() == dayName && s.WorkStart != null);

            if (workingDay == null) continue;
            if (schedule?.Holidays.Any(h => h.HolidayDate.Date == terminalHistoryDateTime.Date) ?? false) continue;

            if (employeeHistory == null) continue;

            // Обновляем существующую запись
            if (employeeHistory.MarkTime >= terminalHistoryDateTime) continue;
            employeeHistory.Status = 1;
            employeeHistory.MarkTime = terminalHistoryDateTime;
            updatedHistories.Add(employeeHistory);
        }

        if (updatedHistories.Any())
        {
            await unitOfWork.NotifyHistoryRepository.UpdateRangeAsync(updatedHistories);
        }
    }

    private async void InitializeBot(string token)
    {
        _telegramService = new TelegramService(token);
        await _telegramService.StartListeningAsync();
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        var ex = e.ExceptionObject as Exception;
        Log.Error(ex, "Unhandled domain exception occurred.");
        ShowErrorMessage("Произошла критическая ошибка. Приложение будет закрыто.");
    }

    private void App_DispatcherUnhandledException(object sender,
        System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unhandled dispatcher exception occurred.");
        ShowErrorMessage("Произошла ошибка. Попробуйте снова.");
        e.Handled = true; // Не завершать приложение
    }

    private void ShowErrorMessage(string message)
    {
        MessageBox.Show(message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    #endregion
}