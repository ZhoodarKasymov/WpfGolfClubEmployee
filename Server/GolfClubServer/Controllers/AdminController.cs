using GolfClubServer.Data;
using GolfClubServer.Data.Migrations;
using GolfClubServer.Models;
using GolfClubServer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Serilog;

namespace GolfClubServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly UnitOfWork _unitOfWork;

    public AdminController(UnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    [HttpGet("zones")]
    public async Task<IActionResult> GetZones()
    {
        var zones = await _unitOfWork.ZoneRepository
            .GetAll(true)
            .Where(w => w.DeletedAt == null)
            .ToListAsync();

        return Ok(zones);
    }

    [HttpDelete("zones/{id}")]
    public async Task<IActionResult> DeleteZone(int id)
    {
        var currentZone = await _unitOfWork.ZoneRepository
            .GetAll()
            .FirstOrDefaultAsync(o => o.Id == id);

        if (currentZone is not null)
        {
            currentZone.DeletedAt = DateTime.Now;
            await _unitOfWork.SaveAsync();
        }

        return Ok();
    }

    [HttpGet("schedules")]
    public async Task<IActionResult> GetSchedules()
    {
        var schedules = await _unitOfWork.ScheduleRepository.GetAll(true)
            .Include(sh => sh.Scheduledays)
            .Include(sh => sh.Holidays)
            .Where(w => w.DeletedAt == null)
            .ToListAsync();

        var response = JsonConvert.SerializeObject(schedules);
        return Ok(response);
    }

    [HttpDelete("schedule/{id}")]
    public async Task<IActionResult> DeleteSchedule(int id)
    {
        var currentSchedule = await _unitOfWork.ScheduleRepository
            .GetAll()
            .FirstOrDefaultAsync(o => o.Id == id);

        if (currentSchedule is not null)
        {
            currentSchedule.DeletedAt = DateTime.Now;
            await _unitOfWork.SaveAsync();
        }

        return Ok();
    }

    [HttpGet("autoSchedules")]
    public async Task<IActionResult> GetAutoSchedules()
    {
        var schedules = await _unitOfWork.NotifyJobRepository
            .GetAll(true)
            .Include(j => j.Zone)
            .Include(j => j.Organization)
            .Include(j => j.Shift)
            .ToListAsync();

        var response = JsonConvert.SerializeObject(schedules);
        return Ok(response);
    }

    [HttpDelete("autoSchedules/{id}")]
    public async Task<IActionResult> DeleteAutoSchedules(int id)
    {
        await _unitOfWork.NotifyJobRepository.DeleteAsync(id);
        return Ok();
    }

    [HttpPost("auto-notify")]
    public async Task<IActionResult> SendNotifications([FromBody] NotifyRequest request)
    {
        var countJob = _unitOfWork.NotifyJobRepository.GetAll().Count();

        if (countJob > 3)
        {
            return BadRequest("Нельзя больше 3 авто уведомлений создать");
        }

        if (request.ShiftId is null)
        {
            return BadRequest(new { Message = "Расписание обьязательное!" });
        }

        List<Worker> selectedWorkers;
        if (request.Percent.HasValue)
        {
            var query = _unitOfWork.WorkerRepository
                .GetAll()
                .Where(w => w.ChatId != null && w.DeletedAt == null && w.EndWork >= DateTime.Now.Date);

            if (request.OrganizationId.HasValue && request.OrganizationId != -1)
            {
                query = query.Where(w => w.OrganizationId == request.OrganizationId.Value);
            }

            if (request.ZoneId.HasValue && request.ZoneId != -1)
            {
                query = query.Where(w => w.ZoneId == request.ZoneId.Value);
            }

            var totalCount = await query.CountAsync();
            var countToFetch = (int)Math.Round(totalCount * (request.Percent.Value / 100m));

            selectedWorkers = await query
                .OrderBy(w => Guid.NewGuid())
                .Take(countToFetch)
                .ToListAsync();
        }
        else if (request.WorkerIds != null && request.WorkerIds.Any())
        {
            selectedWorkers = await _unitOfWork.WorkerRepository
                .GetAll()
                .Where(w => request.WorkerIds.Contains(w.Id) && w.ChatId != null && w.DeletedAt == null &&
                            w.EndWork >= DateTime.Now.Date)
                .ToListAsync();
        }
        else
        {
            return BadRequest(new { Message = "No workers selected or percentage specified" });
        }

        if (!selectedWorkers.Any())
        {
            return BadRequest(new { Message = "Работники не найденны!" });
        }

        var newJob = new NotifyJob
        {
            OrganizationId = request.OrganizationId is null or -1 ? null : request.OrganizationId,
            ZoneId = request.ZoneId is null or -1 ? null : request.ZoneId,
            Message = request.Description,
            ShiftId = request.ShiftId,
            Percentage = request.Percent,
            WorkerIds = JsonConvert.SerializeObject(selectedWorkers.Select(w => w.Id).ToList())
        };

        await _unitOfWork.NotifyJobRepository.AddAsync(newJob);

        return Ok(new { Message = "Notifications sent successfully", NotifiedWorkers = selectedWorkers.Count });
    }

    [HttpPost("zones")]
    public async Task<IActionResult> AddZone([FromBody] Zone zoneDto)
    {
        if (string.IsNullOrWhiteSpace(zoneDto?.Name) || string.IsNullOrWhiteSpace(zoneDto.Login) ||
            string.IsNullOrWhiteSpace(zoneDto.Password) || string.IsNullOrWhiteSpace(zoneDto.EnterIp) ||
            string.IsNullOrWhiteSpace(zoneDto.ExitIp) || string.IsNullOrWhiteSpace(zoneDto.NotifyIp))
        {
            return BadRequest(new { Message = "All zone fields are required" });
        }

        try
        {
            var terminalService = new TerminalService(zoneDto.Login, zoneDto.Password);
            var allActiveWorkers = await _unitOfWork.WorkerRepository
                .GetAll()
                .Where(w => w.DeletedAt == null && w.EndWork >= DateTime.Now.Date)
                .ToListAsync();

            var request = new UserInfoDeleteRequest
            {
                UserInfoDelCond = new UserInfoDelCond
                {
                    EmployeeNoList = []
                }
            };

            try
            {
                await terminalService.DeleteUsersAsync(request, zoneDto.EnterIp);
                await terminalService.DeleteUsersAsync(request, zoneDto.ExitIp);
                await terminalService.DeleteUsersAsync(request, zoneDto.NotifyIp);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error deleting old users from terminal");
                return StatusCode(500, new { Message = "Error deleting old users from terminal" });
            }

            foreach (var worker in allActiveWorkers)
            {
                try
                {
                    await UpdateAddTerminalEmployee(worker, worker.PhotoPath, zoneDto.EnterIp, terminalService);
                    await UpdateAddTerminalEmployee(worker, worker.PhotoPath, zoneDto.ExitIp, terminalService);
                    await UpdateAddTerminalEmployee(worker, worker.PhotoPath, zoneDto.NotifyIp, terminalService);
                }
                catch (Exception ex)
                {
                    Log.Error(ex, $"Error adding worker {worker.Id} to terminal");
                    return StatusCode(500, new { Message = $"Error adding worker {worker.Id} to terminal" });
                }
            }

            await _unitOfWork.ZoneRepository.AddAsync(zoneDto);
            await _unitOfWork.SaveAsync();

            return Ok(new { Message = "Zone added successfully", ZoneId = zoneDto.Id });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error adding zone");
            return StatusCode(500, new { Message = "Error adding zone" });
        }

        async Task UpdateAddTerminalEmployee(Worker worker, string photoPath, string ip,
            TerminalService terminalService)
        {
            var terminalUserAdded = await terminalService.AddUserInfoAsync(worker, ip);
            if (terminalUserAdded)
            {
                worker.PhotoPath = photoPath;
                await terminalService.AddUserImageAsync(worker, ip);
                if (!string.IsNullOrEmpty(worker.CardNumber))
                {
                    await terminalService.AddCardInfoAsync(worker, ip);
                }
            }
        }
    }

    [HttpPut("zones/{id}")]
    public async Task<IActionResult> UpdateZone(int id, [FromBody] Zone zoneDto)
    {
        if (string.IsNullOrWhiteSpace(zoneDto?.Name) || string.IsNullOrWhiteSpace(zoneDto.Login) ||
            string.IsNullOrWhiteSpace(zoneDto.Password) || string.IsNullOrWhiteSpace(zoneDto.EnterIp) ||
            string.IsNullOrWhiteSpace(zoneDto.ExitIp) || string.IsNullOrWhiteSpace(zoneDto.NotifyIp))
        {
            return BadRequest(new { Message = "All zone fields are required" });
        }

        var currentZone = await _unitOfWork.ZoneRepository
            .GetAll()
            .FirstOrDefaultAsync(w => w.Id == id && w.DeletedAt == null);

        if (currentZone == null)
        {
            return NotFound(new { Message = "Zone not found" });
        }

        try
        {
            currentZone.Name = zoneDto.Name;
            currentZone.Login = zoneDto.Login;
            currentZone.Password = zoneDto.Password;
            currentZone.EnterIp = zoneDto.EnterIp;
            currentZone.ExitIp = zoneDto.ExitIp;
            currentZone.NotifyIp = zoneDto.NotifyIp;

            await _unitOfWork.ZoneRepository.UpdateAsync(currentZone);
            await _unitOfWork.SaveAsync();

            return Ok(new { Message = "Zone updated successfully" });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error updating zone");
            return StatusCode(500, new { Message = "Error updating zone" });
        }
    }

    [HttpPost("schedules")]
    public async Task<IActionResult> AddSchedule([FromBody] Schedule scheduleDto)
    {
        if (string.IsNullOrWhiteSpace(scheduleDto?.Name))
        {
            return BadRequest(new { Message = "Имя расписания обьязательное" });
        }

        try
        {
            await _unitOfWork.ScheduleRepository.AddAsync(scheduleDto);

            return Ok(new { Message = "Schedule added successfully", ScheduleId = scheduleDto.Id });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error adding schedule");
            return StatusCode(500, new { Message = "Error adding schedule" });
        }
    }

    [HttpPut("schedules/{id}")]
    public async Task<IActionResult> UpdateSchedule(int id, [FromBody] Schedule scheduleDto)
    {
        if (string.IsNullOrWhiteSpace(scheduleDto?.Name))
        {
            return BadRequest(new { Message = "Имя расписания обьязательное" });
        }

        var currentSchedule = await _unitOfWork.ScheduleRepository
            .GetAll(true)
            .Include(s => s.Scheduledays)
            .Include(s => s.Holidays)
            .FirstOrDefaultAsync(s => s.Id == id && s.DeletedAt == null);

        if (currentSchedule == null)
        {
            return NotFound(new { Message = "Расписание не найдено" });
        }

        try
        {
            currentSchedule.Name = scheduleDto.Name;
            currentSchedule.BreakStart = scheduleDto.BreakStart;
            currentSchedule.BreakEnd = scheduleDto.BreakEnd;
            currentSchedule.PermissibleEarlyLeaveStart = scheduleDto.PermissibleEarlyLeaveStart;
            currentSchedule.PermissibleEarlyLeaveEnd = scheduleDto.PermissibleEarlyLeaveEnd;
            currentSchedule.PermissibleLateTimeStart = scheduleDto.PermissibleLateTimeStart;
            currentSchedule.PermissibleLateTimeEnd = scheduleDto.PermissibleLateTimeEnd;
            currentSchedule.PermissionToLateTime = scheduleDto.PermissionToLateTime;

            currentSchedule.Scheduledays.Clear();
            currentSchedule.Scheduledays = scheduleDto.Scheduledays;

            currentSchedule.Holidays.Clear();
            currentSchedule.Holidays = scheduleDto.Holidays;
            await _unitOfWork.ScheduleRepository.UpdateAsync(currentSchedule);

            return Ok(new { Message = "Schedule updated successfully" });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error updating schedule");
            return StatusCode(500, new { Message = "Error updating schedule" });
        }
    }
}