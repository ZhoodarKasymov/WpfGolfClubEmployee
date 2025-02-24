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

    public string PathByStatus => Status switch
    {
        1 => "/Images/status_tittle.png",
        2 => "/Images/Property 1=Cancelled.png",
        3 => "/Images/Property 1=Default.png",
        4 => "/Images/Property-1.png",
        _ => ""
    };
    
    public string WorkTimeText => LeaveTime.HasValue
        ? $"{(int)(LeaveTime.Value - ArrivalTime).TotalHours} часов"
        : $"{(int)(DateTime.Now - ArrivalTime).TotalHours} часов";

    public string StatusColor => Status switch
    {
        1 => "ForestGreen",
        2 => "Red",
        3 => "DarkOrange",
        4 => "Gray",
        _ => "White"
    };
}
