namespace GolfClubSystem.Models;

public partial class Scheduleday
{
    public uint Id { get; set; }

    public uint? ScheduleId { get; set; }

    public string DayOfWeek { get; set; } = null!;

    public TimeOnly? WorkStart { get; set; }

    public TimeOnly? WorkEnd { get; set; }

    public bool IsSelected { get; set; }

    public virtual Schedule? Schedule { get; set; }

    public string WorkingTimeText => IsSelected ? $"{WorkStart?.ToString("HH:mm")}-{WorkEnd?.ToString("HH:mm")}" : "выходной";
}
