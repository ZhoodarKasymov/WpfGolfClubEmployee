namespace GolfClubSystem.Models;

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

    public virtual ICollection<Scheduleday> Scheduledays { get; set; } = new List<Scheduleday>();

    public virtual ICollection<Worker> Workers { get; set; } = new List<Worker>();
}
