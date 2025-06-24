using System;
using System.Collections.Generic;

namespace GolfClubServer.Data.Migrations;

public partial class NotifyJob
{
    public int Id { get; set; }

    public int? OrganizationId { get; set; }

    public int? ZoneId { get; set; }

    public string? WorkerIds { get; set; }

    public decimal? Percentage { get; set; }

    public string? Message { get; set; }

    public TimeOnly? Time { get; set; }

    public uint? ShiftId { get; set; }

    public virtual Organization? Organization { get; set; }

    public virtual Schedule? Shift { get; set; }

    public virtual Zone? Zone { get; set; }
}
