using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace GolfClubServer.Data.Migrations;

public partial class Employeehistory
{
    public int Id { get; set; }

    public int WorkerId { get; set; }

    public DateTime ArrivalTime { get; set; }

    public DateTime? LeaveTime { get; set; }

    public int Status { get; set; }

    public int? WorkHours { get; set; }

    public DateTime? MarkTime { get; set; }

    public int? MarkZoneId { get; set; }

    public virtual Zone? MarkZone { get; set; }

    public virtual Worker Worker { get; set; } = null!;
    
    [NotMapped]
    public int? LateHours { get; set; }
    
    [NotMapped]
    public int? EarlyHours { get; set; }
    
    [NotMapped]
    public int? NoWorkCount { get; set; }
}
