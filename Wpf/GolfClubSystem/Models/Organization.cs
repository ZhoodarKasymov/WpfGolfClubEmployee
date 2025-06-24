namespace GolfClubSystem.Models;

public partial class Organization
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public int? ParentOrganizationId { get; set; }

    public DateTime? DeletedAt { get; set; }

    public virtual ICollection<Organization> InverseParentOrganization { get; set; } = new List<Organization>();

    public virtual Organization? ParentOrganization { get; set; }

    public virtual ICollection<Worker> Workers { get; set; } = new List<Worker>();
    public virtual ICollection<NotifyJob> NotifyJobs { get; set; } = new List<NotifyJob>();
}
