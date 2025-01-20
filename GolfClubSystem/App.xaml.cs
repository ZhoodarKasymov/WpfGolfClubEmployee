using System.Globalization;
using System.IO;
using System.Windows;
using GolfClubSystem.Context;
using GolfClubSystem.Data;
using GolfClubSystem.Models;
using GolfClubSystem.Services;
using GolfClubSystem.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace GolfClubSystem;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private CancellationTokenSource _cts;

    protected override void OnStartup(StartupEventArgs e)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory()) // Set the base directory to current directory
            .AddJsonFile("appsettings.json") // Read the connection string from appsettings.json
            .Build();

        base.OnStartup(e);

        // Инициализация логгера
        Logger.Initialize();

        // Глобальная обработка ошибок
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        DispatcherUnhandledException += App_DispatcherUnhandledException;

        // Init telegram
        var token = configuration.GetSection("Token").Value;
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
                var zones = unitOfWork.ZoneRepository.GetAll()
                    .AsNoTracking()
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
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, $"Error processing zone {zone.Id}: {ex.Message}");
                    }
                });

                await Task.Delay(TimeSpan.FromSeconds(40), token);
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
            .Where(w => w.DeletedAt == null && workerIds.Contains(w.Id))
            .ToListAsync();

        var existingHistories = await unitOfWork.HistoryRepository.GetAll()
            .AsNoTracking()
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
                else if (schedule.PermissibleLateTimeStart <= terminalHistoryTime && schedule.PermissibleLateTimeEnd >= terminalHistoryTime)
                {
                    newEmployeeHistory.Status = 1;
                }
                else if (terminalHistoryTime > schedule.PermissibleLateTimeEnd && terminalHistoryTime < schedule.PermissionToLateTime)
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

    private async void InitializeBot(string token)
    {
        var telegramService = new TelegramService(token);
        await telegramService.StartListeningAsync();
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