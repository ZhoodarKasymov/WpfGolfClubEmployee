namespace GolfClubSystem.Models;

public partial class NotifyHistory
{
    public int Id { get; set; }

    public int WorkerId { get; set; }

    public DateTime ArrivalTime { get; set; }

    public int Status { get; set; }

    public DateTime? MarkTime { get; set; }

    public virtual Worker Worker { get; set; } = null!;
    
    public string PathByStatus => Status switch
    {
        1 => "/Images/otmetka_status.png",
        2 => "/Images/zapros_status.png",
        _ => ""
    };
    
    public string StatusColor => Status switch
    {
        1 => "ForestGreen",
        2 => "DarkOrange",
        _ => "White"
    };
}
