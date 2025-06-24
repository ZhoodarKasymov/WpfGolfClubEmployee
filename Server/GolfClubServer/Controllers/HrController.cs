using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using GolfClubServer.Data;
using GolfClubServer.Data.Migrations;
using GolfClubServer.Models;
using GolfClubServer.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using Serilog;

namespace GolfClubServer.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HrController : ControllerBase
{
    private readonly UnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;
    private readonly TelegramService _telegramService;

    public HrController(UnitOfWork unitOfWork, IConfiguration configuration, TelegramService telegramService)
    {
        _unitOfWork = unitOfWork;
        _configuration = configuration;
        _telegramService = telegramService;
    }

    [HttpPost("notify")]
    public async Task<IActionResult> SendNotifications([FromBody] NotifyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Description))
        {
            return BadRequest(new { Message = "Description is required" });
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
            return BadRequest(new { Message = "No workers found matching the criteria" });
        }

        var notifyHistory = new List<NotifyHistory>();
        var notifyHistoryExist = new List<NotifyHistory>();

        foreach (var worker in selectedWorkers)
        {
            try
            {
                await _telegramService.SendMessageByUsernameAsync(worker.Id, request.Description);

                var existNotifyHistory = await _unitOfWork.NotifyHistoryRepository
                    .GetAll(true)
                    .FirstOrDefaultAsync(h => h.ArrivalTime.Date == DateTime.Now.Date && h.WorkerId == worker.Id);

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
            catch (Exception ex)
            {
                Log.Error(ex.Message, ex);
            }
        }

        if (notifyHistory.Any())
        {
            await _unitOfWork.NotifyHistoryRepository.AddRangeAsync(notifyHistory);
        }

        if (notifyHistoryExist.Any())
        {
            await _unitOfWork.NotifyHistoryRepository.UpdateRangeAsync(notifyHistoryExist);
        }

        await _unitOfWork.SaveAsync();

        return Ok(new { Message = "Notifications sent successfully", NotifiedWorkers = selectedWorkers.Count });
    }


    [HttpGet("organizations-full")]
    public async Task<IActionResult> GetOrganizationsFull()
    {
        var organizations = await _unitOfWork.OrganizationRepository
            .GetAll()
            .Where(o => o.DeletedAt == null)
            .ToListAsync();

        var json = JsonConvert.SerializeObject(organizations);
        return Ok(json);
    }

    //Organization
    [HttpPost("organizations")]
    public async Task<IActionResult> AddOrganization([FromBody] Organization organizationDto)
    {
        if (string.IsNullOrWhiteSpace(organizationDto.Name))
        {
            return BadRequest(new { Message = "Organization name is required" });
        }

        var exists = await _unitOfWork.OrganizationRepository
            .GetAll()
            .AnyAsync(o => o.Name == organizationDto.Name && o.DeletedAt == null);

        if (exists)
        {
            return BadRequest(new { Message = "Нельзя дублировать названия организаций!" });
        }

        var organization = new Organization
        {
            Name = organizationDto.Name,
            ParentOrganizationId = organizationDto.ParentOrganizationId
        };

        await _unitOfWork.OrganizationRepository.AddAsync(organization);
        await _unitOfWork.SaveAsync();

        return Ok(new { Message = "Organization added successfully", OrganizationId = organization.Id });
    }

    [HttpPut("organizations/{id}")]
    public async Task<IActionResult> UpdateOrganization(int id, [FromBody] Organization organizationDto)
    {
        if (string.IsNullOrWhiteSpace(organizationDto.Name))
        {
            return BadRequest(new { Message = "Organization name is required" });
        }

        var organization = await _unitOfWork.OrganizationRepository
            .GetAll()
            .FirstOrDefaultAsync(o => o.Id == id && o.DeletedAt == null);

        if (organization == null)
        {
            return NotFound(new { Message = "Organization not found" });
        }

        var exists = await _unitOfWork.OrganizationRepository
            .GetAll()
            .AnyAsync(o => o.Name == organizationDto.Name && o.Id != id && o.DeletedAt == null);

        if (exists)
        {
            return BadRequest(new { Message = "Нельзя дублировать названия организаций" });
        }

        organization.Name = organizationDto.Name;
        organization.ParentOrganizationId = organizationDto.ParentOrganizationId;
        await _unitOfWork.OrganizationRepository.UpdateAsync(organization);
        await _unitOfWork.SaveAsync();

        return Ok(new { Message = "Organization updated successfully" });
    }

    [HttpDelete("organizations/{id}")]
    public async Task<IActionResult> DeleteOrganization(int id)
    {
        var organization = await _unitOfWork.OrganizationRepository
            .GetAll()
            .Include(o => o.InverseParentOrganization)
            .Include(o => o.Workers)
            .FirstOrDefaultAsync(o => o.Id == id && o.DeletedAt == null);

        if (organization == null)
        {
            return NotFound(new { Message = "Organization not found" });
        }

        if (organization.Workers.Any(w => w.DeletedAt == null))
        {
            return BadRequest(new { Message = "Cannot delete organization with active workers" });
        }

        MarkAsDeleted(organization);
        await _unitOfWork.OrganizationRepository.UpdateAsync(organization);
        await _unitOfWork.SaveAsync();

        return Ok(new { Message = "Organization deleted successfully" });

        void MarkAsDeleted(Organization org)
        {
            org.DeletedAt = DateTime.UtcNow;
            foreach (var subOrg in org.InverseParentOrganization.Where(o => o.DeletedAt == null))
            {
                MarkAsDeleted(subOrg);
            }
        }
    }

    [HttpGet("schedules")]
    public async Task<IActionResult> GetSchedules()
    {
        var schedules = await _unitOfWork.ScheduleRepository
            .GetAll()
            .Where(s => s.DeletedAt == null)
            .Select(s => new { s.Id, s.Name })
            .ToListAsync();
        return Ok(schedules);
    }

    [HttpPost("images")]
    public async Task<IActionResult> SaveImage(IFormFile image)
    {
        if (image == null || image.Length == 0)
        {
            return BadRequest(new { Message = "No image provided" });
        }

        try
        {
            var ipAddress = _configuration.GetValue<string>("IpAddressImage");
            var directoryPath = _configuration.GetValue<string>("ImagePath");
            var maxSizeKB = 200;

            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            string fileName = Guid.NewGuid().ToString() + ".jpeg";
            string fullPath = Path.Combine(directoryPath, fileName);

            using (var stream = image.OpenReadStream())
            using (var bitmap = new Bitmap(stream))
            {
                var encoderParams = new EncoderParameters(1);
                var qualityParam = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 90L);
                encoderParams.Param[0] = qualityParam;

                var jpegCodec = ImageCodecInfo.GetImageEncoders()
                    .FirstOrDefault(c => c.FormatID == ImageFormat.Jpeg.Guid);

                int quality = 90;
                do
                {
                    using (var outputStream = new MemoryStream())
                    {
                        bitmap.Save(outputStream, jpegCodec, encoderParams);
                        if (outputStream.Length / 1024 > maxSizeKB)
                        {
                            quality -= 5;
                            qualityParam = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, quality);
                            encoderParams.Param[0] = qualityParam;
                        }
                        else
                        {
                            await System.IO.File.WriteAllBytesAsync(fullPath, outputStream.ToArray());
                            break;
                        }
                    }
                } while (quality > 10);
            }

            return Ok(new { ImageUrl = $"http://{ipAddress}/{fileName}" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = $"Error saving image: {ex.Message}" });
        }
    }

    [HttpPost("workers")]
    public async Task<IActionResult> AddWorker([FromForm] string workerJson, IFormFile image)
    {
        if (string.IsNullOrWhiteSpace(workerJson) || image == null)
        {
            return BadRequest(new { Message = "Worker data or image is missing" });
        }

        try
        {
            // Save image
            var imageResponse = await SaveImage(image);
            if (imageResponse is not OkObjectResult imageOkResult)
            {
                return StatusCode(((IStatusCodeActionResult)imageResponse).StatusCode.Value,
                    ((ObjectResult)imageResponse).Value);
            }

            var imageResult = (dynamic)imageOkResult.Value;
            var photoPath = imageResult.ImageUrl;

            var workerDto = JsonConvert.DeserializeObject<Worker>(workerJson);

            // Create worker
            var worker = new Worker
            {
                FullName = workerDto.FullName,
                JobTitle = workerDto.JobTitle,
                OrganizationId = workerDto.OrganizationId,
                ZoneId = workerDto.ZoneId,
                ScheduleId = workerDto.ScheduleId,
                Mobile = workerDto.Mobile,
                CardNumber = workerDto.CardNumber,
                StartWork = workerDto.StartWork,
                EndWork = workerDto.EndWork,
                TelegramUsername = workerDto.TelegramUsername,
                AdditionalMobile = workerDto.AdditionalMobile,
                PhotoPath = photoPath
            };

            await _unitOfWork.WorkerRepository.AddAsync(worker);
            await _unitOfWork.SaveAsync();

            // Update terminals
            var zones = await _unitOfWork.ZoneRepository
                .GetAll()
                .Where(z => z.DeletedAt == null)
                .ToListAsync();

            foreach (var zone in zones)
            {
                using var terminalService = new TerminalService(zone.Login, zone.Password);
                await UpdateAddTerminalEmployee(terminalService, worker, zone.EnterIp);
                await UpdateAddTerminalEmployee(terminalService, worker, zone.ExitIp);
                await UpdateAddTerminalEmployee(terminalService, worker, zone.NotifyIp);
            }

            return Ok(new { Message = "Worker added successfully", WorkerId = worker.Id });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = $"Error adding worker: {ex.Message}" });
        }

        async Task UpdateAddTerminalEmployee(TerminalService terminalService, Worker worker, string ip)
        {
            var terminalUserAdded = await terminalService.AddUserInfoAsync(worker, ip);
            if (terminalUserAdded)
            {
                await terminalService.AddUserImageAsync(worker, ip);
                if (!string.IsNullOrEmpty(worker.CardNumber))
                {
                    await terminalService.AddCardInfoAsync(worker, ip);
                }
            }
        }
    }

    [HttpPut("workers/{id}")]
    public async Task<IActionResult> UpdateWorker(int id, [FromForm] string workerJson, IFormFile image)
    {
        if (string.IsNullOrWhiteSpace(workerJson) || image == null)
        {
            return BadRequest(new { Message = "Worker data or image is missing" });
        }

        try
        {
            var existingWorker = await _unitOfWork.WorkerRepository
                .GetAll()
                .FirstOrDefaultAsync(w => w.Id == id && w.DeletedAt == null);

            if (existingWorker == null)
            {
                return NotFound(new { Message = "Worker not found" });
            }

            var workerDto = JsonConvert.DeserializeObject<Worker>(workerJson);

            // Save new image
            var imageResponse = await SaveImage(image);
            if (imageResponse is not OkObjectResult imageOkResult)
            {
                return StatusCode(((IStatusCodeActionResult)imageResponse).StatusCode.Value,
                    ((ObjectResult)imageResponse).Value);
            }

            var imageResult = (dynamic)imageOkResult.Value;
            var photoPath = imageResult.ImageUrl;

            // Update worker
            existingWorker.FullName = workerDto.FullName;
            existingWorker.JobTitle = workerDto.JobTitle;
            existingWorker.OrganizationId = workerDto.OrganizationId;
            existingWorker.ZoneId = workerDto.ZoneId;
            existingWorker.ScheduleId = workerDto.ScheduleId;
            existingWorker.Mobile = workerDto.Mobile;
            existingWorker.CardNumber = workerDto.CardNumber;
            existingWorker.StartWork = workerDto.StartWork;
            existingWorker.EndWork = workerDto.EndWork;
            existingWorker.TelegramUsername = workerDto.TelegramUsername;
            existingWorker.AdditionalMobile = workerDto.AdditionalMobile;
            existingWorker.PhotoPath = photoPath;

            await _unitOfWork.WorkerRepository.UpdateAsync(existingWorker);
            await _unitOfWork.SaveAsync();

            // Update terminals
            var zones = await _unitOfWork.ZoneRepository
                .GetAll()
                .Where(z => z.DeletedAt == null)
                .ToListAsync();

            foreach (var zone in zones)
            {
                using var terminalService = new TerminalService(zone.Login, zone.Password);
                await terminalService.DeleteUserImageAsync(existingWorker.Id.ToString(), zone.EnterIp);
                await terminalService.DeleteUserImageAsync(existingWorker.Id.ToString(), zone.ExitIp);
                await terminalService.DeleteUserImageAsync(existingWorker.Id.ToString(), zone.NotifyIp);
                await UpdateAddTerminalEmployee(terminalService, existingWorker, zone.EnterIp);
                await UpdateAddTerminalEmployee(terminalService, existingWorker, zone.ExitIp);
                await UpdateAddTerminalEmployee(terminalService, existingWorker, zone.NotifyIp);
            }

            return Ok(new { Message = "Worker updated successfully" });

            async Task UpdateAddTerminalEmployee(TerminalService terminalService, Worker worker, string ip)
            {
                var terminalUserAdded = await terminalService.AddUserInfoAsync(worker, ip);
                if (terminalUserAdded)
                {
                    await terminalService.AddUserImageAsync(worker, ip);
                    if (!string.IsNullOrEmpty(worker.CardNumber))
                    {
                        await terminalService.AddCardInfoAsync(worker, ip);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { Message = $"Error updating worker: {ex.Message}" });
        }
    }

    // Workers
    [HttpGet("zones")]
    public async Task<IActionResult> GetZones()
    {
        var zones = await _unitOfWork.ZoneRepository
            .GetAll()
            .Where(z => z.DeletedAt == null)
            .Select(z => new { z.Id, z.Name })
            .ToListAsync();
        zones.Insert(0, new { Id = -1, Name = "Все" });
        return Ok(zones);
    }

    [HttpGet("workers-paged")]
    public async Task<IActionResult> GetPagedWorkers(
        [FromQuery] string? search,
        [FromQuery] int? organizationId,
        [FromQuery] int? zoneId,
        [FromQuery] DateTime? endWorkDate,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        var query = _unitOfWork.WorkerRepository
            .GetAll()
            .Include(w => w.Organization)
            .Include(w => w.Zone)
            .Include(w => w.Schedule)
            .Where(w => w.DeletedAt == null)
            .AsNoTracking();

        if (endWorkDate is not null)
        {
            query = query.Where(w => w.EndWork >= endWorkDate);
        }

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(w => w.FullName.Contains(search) || w.JobTitle.Contains(search));
        }

        if (organizationId.HasValue && organizationId != -1)
        {
            query = query.Where(w => w.OrganizationId == organizationId.Value);
        }

        if (zoneId.HasValue && zoneId != -1)
        {
            query = query.Where(w => w.ZoneId == zoneId.Value);
        }

        var totalCount = await query.CountAsync();

        var workers = await query
            .OrderBy(w => w.FullName)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var response = new
        {
            TotalCount = totalCount,
            Workers = workers,
            CurrentPage = pageNumber,
            PageSize = pageSize
        };

        var json = JsonConvert.SerializeObject(response);
        return Ok(json);
    }

    [HttpDelete("workers/{id}")]
    public async Task<IActionResult> DeleteWorker(int id)
    {
        var worker = await _unitOfWork.WorkerRepository
            .GetAll()
            .FirstOrDefaultAsync(w => w.Id == id && w.DeletedAt == null);

        if (worker == null)
        {
            return NotFound(new { Message = "Worker not found" });
        }

        var zones = _unitOfWork.ZoneRepository
            .GetAll(true)
            .Where(z => z.DeletedAt == null)
            .ToList();
        worker.DeletedAt = DateTime.UtcNow;
        await _unitOfWork.WorkerRepository.UpdateAsync(worker);

        foreach (var zone in zones)
        {
            using var terminalService = new TerminalService(zone.Login, zone.Password);

            await terminalService.DeleteUsersAsync(new UserInfoDeleteRequest
            {
                UserInfoDelCond = new UserInfoDelCond
                {
                    EmployeeNoList =
                    [
                        new EmployeeNoList
                        {
                            EmployeeNo = worker.Id.ToString()
                        }
                    ]
                }
            }, zone.EnterIp);

            await terminalService.DeleteUsersAsync(new UserInfoDeleteRequest
            {
                UserInfoDelCond = new UserInfoDelCond
                {
                    EmployeeNoList =
                    [
                        new EmployeeNoList
                        {
                            EmployeeNo = worker.Id.ToString()
                        }
                    ]
                }
            }, zone.ExitIp);

            await terminalService.DeleteUsersAsync(new UserInfoDeleteRequest
            {
                UserInfoDelCond = new UserInfoDelCond
                {
                    EmployeeNoList =
                    [
                        new EmployeeNoList
                        {
                            EmployeeNo = worker.Id.ToString()
                        }
                    ]
                }
            }, zone.NotifyIp);
        }

        return Ok(new { Message = "Worker deleted successfully" });
    }

    [HttpGet("notify-history-paged")]
    public async Task<IActionResult> GetPagedNotifyHistory(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] int? organizationId,
        [FromQuery] int? statusId,
        [FromQuery] string? search,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        var query = _unitOfWork.NotifyHistoryRepository
            .GetAll()
            .Include(h => h.Worker)
            .ThenInclude(w => w.Organization)
            .Include(h => h.Worker)
            .ThenInclude(w => w.Zone)
            .Include(h => h.Worker)
            .ThenInclude(w => w.Schedule)
            .AsNoTracking();

        if (startDate.HasValue && endDate.HasValue)
        {
            query = query.Where(h => h.ArrivalTime >= startDate.Value && h.ArrivalTime <= endDate.Value);
        }

        if (organizationId.HasValue && organizationId != -1)
        {
            query = query.Where(h => h.Worker.OrganizationId == organizationId.Value);
        }

        if (statusId.HasValue && statusId != -1)
        {
            query = query.Where(h => h.Status == statusId.Value);
        }

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(h => h.Worker.FullName.Contains(search) || h.Worker.JobTitle.Contains(search));
        }

        var totalCount = await query.CountAsync();

        var histories = await query
            .OrderByDescending(h => h.ArrivalTime)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var response = new
        {
            TotalCount = totalCount,
            Histories = histories,
            CurrentPage = pageNumber,
            PageSize = pageSize
        };

        var json = JsonConvert.SerializeObject(response);
        return Ok(json);
    }

    [HttpGet("history-paged")]
    public async Task<IActionResult> GetPagedHistory(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] int? organizationId,
        [FromQuery] int? statusId,
        [FromQuery] string? search,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 10)
    {
        var query = _unitOfWork.HistoryRepository
            .GetAll()
            .Include(h => h.MarkZone)
            .Include(h => h.Worker)
            .ThenInclude(w => w.Organization)
            .Include(h => h.Worker)
            .ThenInclude(w => w.Zone)
            .Include(h => h.Worker)
            .ThenInclude(w => w.Schedule)
            .AsNoTracking();

        if (startDate.HasValue && endDate.HasValue)
        {
            query = query.Where(h => h.ArrivalTime >= startDate.Value && h.ArrivalTime <= endDate.Value);
        }

        if (organizationId.HasValue && organizationId != -1)
        {
            query = query.Where(h => h.Worker.OrganizationId == organizationId.Value);
        }

        if (statusId.HasValue && statusId != -1)
        {
            query = query.Where(h => h.Status == statusId.Value);
        }

        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(h => h.Worker.FullName.Contains(search) || h.Worker.JobTitle.Contains(search));
        }

        var totalCount = await query.CountAsync();

        var histories = await query
            .OrderByDescending(h => h.ArrivalTime)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var response = new
        {
            TotalCount = totalCount,
            Histories = histories,
            CurrentPage = pageNumber,
            PageSize = pageSize
        };

        var json = JsonConvert.SerializeObject(response);
        return Ok(json);
    }


    [HttpGet("organizations")]
    public async Task<IActionResult> GetOrganizations()
    {
        var organizations = await _unitOfWork.OrganizationRepository
            .GetAll()
            .Where(o => o.DeletedAt == null)
            .Select(o => new { o.Id, o.Name })
            .ToListAsync();
        organizations.Insert(0, new { Id = -1, Name = "Все" });
        return Ok(organizations);
    }

    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboardData([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate,
        [FromQuery] int? organizationId)
    {
        var historyQuery = _unitOfWork.HistoryRepository
            .GetAll(true)
            .Include(h => h.Worker)
            .Include(h => h.MarkZone)
            .AsQueryable();

        var notifyHistoryQuery = _unitOfWork.NotifyHistoryRepository
            .GetAll(true)
            .Include(h => h.Worker)
            .AsQueryable();

        var workersQuery = _unitOfWork.WorkerRepository
            .GetAll(true)
            .Where(w => w.DeletedAt == null)
            .Include(w => w.Schedule)
            .ThenInclude(s => s.Scheduledays)
            .Include(w => w.Schedule)
            .ThenInclude(s => s.Holidays)
            .AsQueryable();

        if (startDate.HasValue && endDate.HasValue)
        {
            historyQuery = historyQuery.Where(h => h.ArrivalTime >= startDate.Value && h.ArrivalTime <= endDate.Value);
            notifyHistoryQuery =
                notifyHistoryQuery.Where(h => h.ArrivalTime >= startDate.Value && h.ArrivalTime <= endDate.Value);
        }

        if (organizationId.HasValue && organizationId != -1)
        {
            historyQuery = historyQuery.Where(h => h.Worker.OrganizationId == organizationId.Value);
            notifyHistoryQuery = notifyHistoryQuery.Where(h => h.Worker.OrganizationId == organizationId.Value);
            workersQuery = workersQuery.Where(w => w.OrganizationId == organizationId.Value);
        }

        var history = await historyQuery.ToListAsync();
        var notifyHistory = await notifyHistoryQuery.ToListAsync();
        var workers = await workersQuery.ToListAsync();

        var groupedHistory = history.GroupBy(h => h.MarkZoneId).ToList();
        var totalEmployees = notifyHistory.Count;
        var trackedCount = notifyHistory.Count(w => w.Status == 1);
        var notifyCount = notifyHistory.Count(w => w.Status == 2);

        var percentageMoreOrEqual8Hours = totalEmployees > 0 ? (double)trackedCount / totalEmployees * 100 : 0;
        var percentageLessThan8Hours = totalEmployees > 0 ? (double)notifyCount / totalEmployees * 100 : 0;
        var percent = (int)(percentageMoreOrEqual8Hours + percentageLessThan8Hours);

        var noWorkers = 0;
        if (startDate.HasValue && endDate.HasValue)
        {
            var daysInRange = (endDate.Value - startDate.Value).Days;
            if (daysInRange > 1) // Skip for Today filter
            {
                for (var date = startDate.Value.Date; date < endDate.Value.Date; date = date.AddDays(1))
                {
                    var startOfDay = date;
                    var endOfDay = date.AddHours(23).AddMinutes(59).AddSeconds(59);

                    var workersInHistory = history
                        .Where(h => h.ArrivalTime >= startOfDay && h.ArrivalTime <= endOfDay)
                        .Select(h => h.WorkerId)
                        .Distinct()
                        .ToList();

                    foreach (var worker in workers)
                    {
                        var shift = worker.Schedule;
                        if (shift == null || shift.Scheduledays == null) continue;

                        var shiftDay = shift.Scheduledays.FirstOrDefault(sd =>
                            sd.DayOfWeek.ToLower() == date.ToString("dddd", new CultureInfo("ru-RU")).ToLower());
                        if (shiftDay == null || !shiftDay.IsSelected) continue;

                        if (shift.Holidays?.Any(h => h.HolidayDate.Date == date.Date) ?? false) continue;

                        TimeSpan startTimeSpan, endTimeSpan;
                        DateTime shiftStart, shiftEnd;
                        if (shiftDay.WorkStart > shiftDay.WorkEnd) // Night shift
                        {
                            var nightStart = shiftDay.WorkStart.Value;
                            var nightEnd = shiftDay.WorkEnd.Value;

                            startTimeSpan = nightStart.ToTimeSpan();
                            endTimeSpan = nightEnd.ToTimeSpan();

                            var now = TimeOnly.FromDateTime(DateTime.Now);
                            if (now >= nightStart)
                            {
                                shiftStart = date + startTimeSpan;
                                shiftEnd = date.AddDays(1) + endTimeSpan;
                            }
                            else
                            {
                                shiftStart = date.AddDays(-1) + startTimeSpan;
                                shiftEnd = date + endTimeSpan;
                            }
                        }
                        else // Day shift
                        {
                            startTimeSpan = shiftDay.WorkStart.Value.ToTimeSpan();
                            endTimeSpan = shiftDay.WorkEnd.Value.ToTimeSpan();
                            shiftStart = date + startTimeSpan;
                            shiftEnd = date + endTimeSpan;
                        }

                        if (shiftStart <= endOfDay && shiftEnd >= startOfDay && !workersInHistory.Contains(worker.Id))
                        {
                            noWorkers++;
                        }
                    }
                }
            }
        }

        var response = new
        {
            DonutPercent = $"{percent}%",
            DonutTrackedCount = trackedCount.ToString(),
            DonutNotifyCount = notifyCount.ToString(),
            PieChartData = new[]
            {
                new { Title = "Отметились", Value = Math.Round(percentageMoreOrEqual8Hours), Color = "#00BE55" },
                new { Title = "Запросов", Value = Math.Round(percentageLessThan8Hours), Color = "#EE4545" }
            },
            BarChartData = groupedHistory.Select(g => new
            {
                Count = g.Count(),
                ZoneName = g.First().MarkZone?.Name ?? ""
            }),
            InTime = history.Count(h => h.Status == 1),
            VeryLate = history.Count(h => h.Status == 2),
            Late = history.Count(h => h.Status == 3),
            EarlyLeave = history.Count(h => h.Status == 4),
            NoWorkers = noWorkers
        };

        return Ok(response);
    }
}