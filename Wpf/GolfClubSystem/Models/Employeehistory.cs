using System.ComponentModel.DataAnnotations.Schema;
using System.Drawing;

namespace GolfClubSystem.Models;

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

    public string PathByStatus => Status switch
    {
        1 => "/Images/prishel.png",
        2 => "/Images/ne_prishol.png",
        3 => "/Images/opozdal.png",
        4 => "/Images/ushel_ranshe.png",
        _ => ""
    };
    
    public string WorkTimeText => $"{WorkHours} часов";

    public string StatusColor => Status switch
    {
        1 => "ForestGreen",
        2 => "Red",
        3 => "DarkOrange",
        4 => "Gray",
        _ => "White"
    };
}
