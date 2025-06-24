using System;
using System.Collections.Generic;

namespace GolfClubServer.Data.Migrations;

public partial class NotifyHistory
{
    public int Id { get; set; }

    public int WorkerId { get; set; }

    public DateTime ArrivalTime { get; set; }

    public int Status { get; set; }

    public DateTime? MarkTime { get; set; }

    public virtual Worker Worker { get; set; } = null!;
}
