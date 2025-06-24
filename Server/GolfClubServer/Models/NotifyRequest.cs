namespace GolfClubServer.Models;

public class NotifyRequest
{
    public string Description { get; set; }
    public int? Percent { get; set; }
    public int[] WorkerIds { get; set; }
    public int? OrganizationId { get; set; }
    public int? ZoneId { get; set; }
    
    public uint? ShiftId { get; set; }
}