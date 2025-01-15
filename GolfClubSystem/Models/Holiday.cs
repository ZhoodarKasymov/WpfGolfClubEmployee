namespace GolfClubSystem.Models;

public partial class Holiday
{
    public ulong Id { get; set; }

    public uint? ScheduleId { get; set; }

    public DateOnly HolidayDate { get; set; }

    public string? Description { get; set; }

    public virtual Schedule? Schedule { get; set; }
}
