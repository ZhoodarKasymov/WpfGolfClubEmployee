using System.ComponentModel.DataAnnotations.Schema;

namespace GolfClubSystem.Models;

public partial class Worker
{
    public string FullName { get; set; } = null!;

    public string JobTitle { get; set; } = null!;

    public long? ChatId { get; set; }

    public int? OrganizationId { get; set; }

    public string? TelegramUsername { get; set; }

    public string? Mobile { get; set; }

    public string? AdditionalMobile { get; set; }

    public string? CardNumber { get; set; }

    public string? PhotoPath { get; set; }

    public int? ZoneId { get; set; }

    public DateTime? DeletedAt { get; set; }

    public int? ShiftId { get; set; }

    public DateTime StartWork { get; set; }

    public DateTime? EndWork { get; set; }

    public int Id { get; set; }

    public virtual Organization? Organization { get; set; }

    public virtual Shift? Shift { get; set; }

    public virtual Zone? Zone { get; set; }

    [NotMapped]
    public bool IsSelected { get; set; }
}
