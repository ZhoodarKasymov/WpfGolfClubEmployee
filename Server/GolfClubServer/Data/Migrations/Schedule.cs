using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace GolfClubServer.Data.Migrations;

public partial class Schedule
{
    public uint Id { get; set; }

    public string Name { get; set; } = null!;

    public TimeOnly? PermissibleLateTimeStart { get; set; }

    public TimeOnly? PermissibleEarlyLeaveStart { get; set; }

    public DateTime? DeletedAt { get; set; }

    public TimeOnly? BreakStart { get; set; }

    public TimeOnly? BreakEnd { get; set; }

    public TimeOnly? PermissibleLateTimeEnd { get; set; }

    public TimeOnly? PermissibleEarlyLeaveEnd { get; set; }

    public TimeOnly? PermissionToLateTime { get; set; }
    
    public virtual ICollection<Holiday> Holidays { get; set; } = new List<Holiday>();

    [JsonIgnore]
    public virtual ICollection<NotifyJob> NotifyJobs { get; set; } = new List<NotifyJob>();
    
    public virtual ICollection<Scheduleday> Scheduledays { get; set; } = new List<Scheduleday>();

    [JsonIgnore]
    public virtual ICollection<Worker> Workers { get; set; } = new List<Worker>();
}
