using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace GolfClubServer.Data.Migrations;

public partial class Organization
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public int? ParentOrganizationId { get; set; }

    public DateTime? DeletedAt { get; set; }

    [JsonIgnore]
    public virtual ICollection<Organization> InverseParentOrganization { get; set; } = new List<Organization>();

    [JsonIgnore]
    public virtual ICollection<NotifyJob> NotifyJobs { get; set; } = new List<NotifyJob>();

    [JsonIgnore]
    public virtual Organization? ParentOrganization { get; set; }

    [JsonIgnore]
    public virtual ICollection<Worker> Workers { get; set; } = new List<Worker>();
}
