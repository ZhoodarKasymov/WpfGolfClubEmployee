using System.Globalization;
using System.Text;
using System.Text.Json;
using GolfClubServer.Data;
using GolfClubServer.Data.Migrations;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace GolfClubServer.Services;

public class TrackingService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly TelegramService _telegramService;
    private readonly Dictionary<(int JobId, DateTime Date), bool> _jobExecutionLog = new();

    public TrackingService(IServiceProvider serviceProvider, TelegramService telegramService)
    {
        _serviceProvider = serviceProvider;
        _telegramService = telegramService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var notifyTask = BackgroundNotifyTask(stoppingToken);
        var backgroundTask = BackgroundTask(stoppingToken);

        await Task.WhenAll(notifyTask, backgroundTask);
    }

    private async Task BackgroundNotifyTask(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var currentDate = DateTime.Now.Date;
                var dayName = currentDate.ToString("dddd", new CultureInfo("ru-RU")).ToLower();
                var now = TimeOnly.FromDateTime(DateTime.Now);

                using var scope = _serviceProvider.CreateScope();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<UnitOfWork>();
                var jobs = await unitOfWork.NotifyJobRepository.GetAll()
                    .Include(j => j.Shift)
                    .ThenInclude(s => s.Scheduledays)
                    .Where(n => n.Shift.Scheduledays.Any(sd => sd.DayOfWeek.ToLower() == dayName))
                    .ToListAsync(token);

                await Task.WhenAll(jobs.Select(job => ProcessJobAsync(job, currentDate, now, token)));

                var timeToNextDay = (currentDate.AddDays(1) - DateTime.Now).TotalMilliseconds;
                await Task.Delay(TimeSpan.FromMilliseconds(Math.Max(timeToNextDay, 60000)), token);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in background notification task");
            }
        }
    }

    private async Task ProcessJobAsync(NotifyJob job, DateTime currentDate, TimeOnly now, CancellationToken token)
    {
        var jobKey = (job.Id, currentDate);

        lock (_jobExecutionLog)
        {
            if (_jobExecutionLog.ContainsKey(jobKey))
                return;
            _jobExecutionLog[jobKey] = true;
        }

        var shiftDay = job.Shift.Scheduledays.FirstOrDefault(sd =>
            sd.DayOfWeek.ToLower() == currentDate.ToString("dddd", new CultureInfo("ru-RU")).ToLower());
        if (shiftDay is not { IsSelected: true }) return;

        if (job.Shift?.Holidays.Any(h => h.HolidayDate.Date == currentDate.Date) ?? false) return;

        TimeSpan startTimeSpan, endTimeSpan;
        if (shiftDay.WorkStart > shiftDay.WorkEnd)
        {
            var nightStart = shiftDay.WorkStart.Value;
            var nightEnd = shiftDay.WorkEnd.Value;

            startTimeSpan = nightStart.ToTimeSpan();
            endTimeSpan = nightEnd.ToTimeSpan();

            if (now >= nightStart)
                endTimeSpan = endTimeSpan.Add(TimeSpan.FromDays(1));
            else
                startTimeSpan = startTimeSpan.Add(TimeSpan.FromDays(-1));
        }
        else
        {
            startTimeSpan = shiftDay.WorkStart.Value.ToTimeSpan();
            endTimeSpan = shiftDay.WorkEnd.Value.ToTimeSpan();
            if (now >= shiftDay.WorkEnd.Value) return;
        }

        var currentTimeSpan = now.ToTimeSpan();
        if (currentTimeSpan >= endTimeSpan) return;

        var randomTime = GenerateRandomTime(
            currentTimeSpan > startTimeSpan ? currentTimeSpan : startTimeSpan,
            endTimeSpan);

        var delay = randomTime > currentTimeSpan ? randomTime - currentTimeSpan : TimeSpan.Zero;
        if (delay > TimeSpan.Zero)
            await Task.Delay(delay, token);

        if (token.IsCancellationRequested) return;

        using var scope = _serviceProvider.CreateScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<UnitOfWork>();
        List<Worker> selectedWorkers = await GetWorkersForJobAsync(job, unitOfWork, token);

        if (selectedWorkers.Any())
        {
            List<NotifyHistory> notifyHistory = [];
            List<NotifyHistory> notifyHistoryExist = [];

            foreach (var worker in selectedWorkers)
            {
                await _telegramService.SendMessageByUsernameAsync(worker.Id, job.Message);

                var existNotifyHistory = await unitOfWork.NotifyHistoryRepository.GetAll(true)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(h => h.ArrivalTime.Date == currentDate && h.WorkerId == worker.Id, token);

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
                await unitOfWork.NotifyHistoryRepository.AddRangeAsync(notifyHistory);

            if (notifyHistoryExist.Any())
                await unitOfWork.NotifyHistoryRepository.UpdateRangeAsync(notifyHistoryExist);
        }
    }

    private async Task<List<Worker>> GetWorkersForJobAsync(NotifyJob job, UnitOfWork unitOfWork, CancellationToken token)
    {
        var nowDate = DateTime.Now.Date;

        if (job.Percentage.HasValue)
        {
            var totalCountQuery = unitOfWork.WorkerRepository.GetAll()
                .Where(w => w.Employeehistories.Any(h => h.ArrivalTime.Date == nowDate) && w.ChatId != null && w.DeletedAt == null && w.EndWork >= DateTime.Now.Date);

            if (job.OrganizationId.HasValue)
                totalCountQuery = totalCountQuery.Where(w => w.OrganizationId == job.OrganizationId.Value);

            if (job.ZoneId.HasValue)
                totalCountQuery = totalCountQuery.Where(w => w.ZoneId == job.ZoneId.Value);

            var totalCount = await totalCountQuery.CountAsync(token);
            var countToFetch = (int)Math.Round(totalCount * (job.Percentage.Value / 100m));

            return await totalCountQuery
                .OrderBy(w => Guid.NewGuid())
                .Take(countToFetch)
                .ToListAsync(token);
        }
        else
        {
            var workerIds = JsonSerializer.Deserialize<List<int>>(job.WorkerIds);
            return await unitOfWork.WorkerRepository.GetAll()
                .Where(w => w.Employeehistories.Any(h => h.ArrivalTime.Date == nowDate) && w.ChatId != null && workerIds.Contains(w.Id) && w.EndWork >= DateTime.Now.Date && w.DeletedAt == null)
                .ToListAsync(token);
        }
    }

    private TimeSpan GenerateRandomTime(TimeSpan start, TimeSpan end)
    {
        var random = new Random();
        var range = (end - start).TotalMilliseconds;
        var randomMilliseconds = random.NextDouble() * range;
        return start + TimeSpan.FromMilliseconds(randomMilliseconds);
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

                using var scope = _serviceProvider.CreateScope();
                var unitOfWork = scope.ServiceProvider.GetRequiredService<UnitOfWork>();
                var zones = await unitOfWork.ZoneRepository.GetAll(true)
                    .Where(z => z.DeletedAt == null)
                    .ToListAsync(token);

                await Parallel.ForEachAsync(zones, options, async (zone, ct) =>
                {
                    try
                    {
                        using var scope = _serviceProvider.CreateScope();
                        var zoneUnitOfWork = scope.ServiceProvider.GetRequiredService<UnitOfWork>();
                        using var terminalService = new TerminalService(zone.Login, zone.Password);

                        await ProcessZone(zone, zoneUnitOfWork, terminalService, false, ct);
                        await ProcessZone(zone, zoneUnitOfWork, terminalService, true, ct);
                        await ProcessNotifyZone(zone.NotifyIp, zoneUnitOfWork, terminalService, ct);
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
            }
        }
    }

    private async Task ProcessZone(Zone zone, UnitOfWork unitOfWork, TerminalService terminalService, bool isExitIp, CancellationToken token)
    {
        var nowDate = DateTime.Now;
        var ip = isExitIp ? zone.ExitIp : zone.EnterIp;

        var histories = await terminalService.GetFilteredUserHistoriesAsync(ip, nowDate.ToString("yyyy-MM-dd"));
        if (histories.Count == 0) return;

        var groupedHistories = histories
            .Where(h => !string.IsNullOrEmpty(h.employeeNoString) && !string.IsNullOrEmpty(h.time))
            .GroupBy(h => int.Parse(h.employeeNoString))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(h => DateTimeOffset.Parse(h.time).DateTime).ToList());

        var workerIds = groupedHistories.Keys.ToList();
        var workers = await unitOfWork.WorkerRepository.GetAll()
            .AsNoTracking()
            .Include(w => w.Schedule)
            .ThenInclude(sc => sc.Scheduledays)
            .Include(w => w.Schedule)
            .ThenInclude(sc => sc.Holidays)
            .Where(w => w.DeletedAt == null && workerIds.Contains(w.Id) && w.EndWork >= nowDate.Date)
            .ToListAsync(token);

        var existingHistories = await unitOfWork.HistoryRepository.GetAll()
            .Where(h => h.ArrivalTime.Date == nowDate.Date && workerIds.Contains(h.WorkerId))
            .ToListAsync(token);

        var newHistories = new List<Employeehistory>();
        var updatedHistories = new List<Employeehistory>();

        foreach (var worker in workers)
        {
            if (!groupedHistories.TryGetValue(worker.Id, out var workerHistories)) continue;

            var latestHistory = workerHistories.First();
            var terminalHistoryDateTime = DateTimeOffset.Parse(latestHistory.time).DateTime;
            var terminalHistoryTime = TimeOnly.FromDateTime(terminalHistoryDateTime);

            var employeeHistory = existingHistories.FirstOrDefault(h => h.WorkerId == worker.Id);

            var schedule = worker.Schedule;
            var dayName = nowDate.ToString("dddd", new CultureInfo("ru-RU")).ToLower();
            var workingDay = schedule?.Scheduledays.FirstOrDefault(s => s.DayOfWeek.ToLower() == dayName && s.WorkStart != null);

            if (workingDay is not { IsSelected: true }) continue;
            if (schedule?.Holidays.Any(h => h.HolidayDate.Date == terminalHistoryDateTime.Date) ?? false) continue;

            if (employeeHistory != null)
            {
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

    private async Task ProcessNotifyZone(string ipAddress, UnitOfWork unitOfWork, TerminalService terminalService, CancellationToken token)
    {
        var nowDate = DateTime.Now;
        var ip = ipAddress;

        var histories = await terminalService.GetFilteredUserHistoriesAsync(ip, nowDate.ToString("yyyy-MM-dd"));
        if (histories.Count == 0) return;

        var groupedHistories = histories
            .Where(h => !string.IsNullOrEmpty(h.employeeNoString) && !string.IsNullOrEmpty(h.time))
            .GroupBy(h => int.Parse(h.employeeNoString))
            .ToDictionary(g => g.Key, g => g.OrderByDescending(h => DateTimeOffset.Parse(h.time).DateTime).ToList());

        var workerIds = groupedHistories.Keys.ToList();
        var workers = await unitOfWork.WorkerRepository.GetAll()
            .AsNoTracking()
            .Include(w => w.Schedule)
            .ThenInclude(sc => sc.Scheduledays)
            .Include(w => w.Schedule)
            .ThenInclude(sc => sc.Holidays)
            .Where(w => w.DeletedAt == null && workerIds.Contains(w.Id) && w.EndWork >= nowDate.Date)
            .ToListAsync(token);

        var existingHistories = await unitOfWork.NotifyHistoryRepository.GetAll()
            .Where(h => h.ArrivalTime.Date == nowDate.Date && workerIds.Contains(h.WorkerId))
            .ToListAsync(token);

        var updatedHistories = new List<NotifyHistory>();

        foreach (var worker in workers)
        {
            if (!groupedHistories.TryGetValue(worker.Id, out var workerHistories)) continue;

            var latestHistory = workerHistories.First();
            var terminalHistoryDateTime = DateTimeOffset.Parse(latestHistory.time).DateTime;

            var employeeHistory = existingHistories.FirstOrDefault(h => h.WorkerId == worker.Id);

            var schedule = worker.Schedule;
            var dayName = nowDate.ToString("dddd", new CultureInfo("ru-RU")).ToLower();
            var workingDay = schedule?.Scheduledays.FirstOrDefault(s => s.DayOfWeek.ToLower() == dayName && s.WorkStart != null);

            if (workingDay is not { IsSelected: true }) continue;
            if (schedule?.Holidays.Any(h => h.HolidayDate.Date == terminalHistoryDateTime.Date) ?? false) continue;

            if (employeeHistory == null) continue;

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
}