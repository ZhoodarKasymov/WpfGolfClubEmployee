namespace GolfClubSystem.Models;

public partial class Zone
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string EnterIp { get; set; } = null!;

    public string ExitIp { get; set; } = null!;

    public string Login { get; set; } = null!;

    public string Password { get; set; } = null!;

    public virtual ICollection<Worker> Workers { get; set; } = new List<Worker>();
}
