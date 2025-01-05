namespace GolfClubSystem.Models;

public partial class Shift
{
    public int Id { get; set; }

    public string ShiftType { get; set; } = null!;

    public string ShiftDayOfWeek { get; set; } = null!;

    public TimeOnly StartTime { get; set; }

    public TimeOnly EndTime { get; set; }

    public TimeOnly? BreakStart { get; set; }

    public TimeOnly? BreakEnd { get; set; }

    public string? Notes { get; set; }

    public virtual ICollection<Worker> Workers { get; set; } = new List<Worker>();
}
