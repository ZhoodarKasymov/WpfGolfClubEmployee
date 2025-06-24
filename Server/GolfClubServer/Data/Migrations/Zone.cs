using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace GolfClubServer.Data.Migrations;

public partial class Zone
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string EnterIp { get; set; } = null!;

    public string ExitIp { get; set; } = null!;

    public string Login { get; set; } = null!;

    public string Password { get; set; } = null!;

    public DateTime? DeletedAt { get; set; }

    public string NotifyIp { get; set; } = null!;

    [JsonIgnore]
    public virtual ICollection<Employeehistory> Employeehistories { get; set; } = new List<Employeehistory>();

    [JsonIgnore]
    public virtual ICollection<NotifyJob> NotifyJobs { get; set; } = new List<NotifyJob>();

    [JsonIgnore]
    public virtual ICollection<Worker> Workers { get; set; } = new List<Worker>();
}
