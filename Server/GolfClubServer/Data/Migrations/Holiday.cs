using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace GolfClubServer.Data.Migrations;

public partial class Holiday
{
    public ulong Id { get; set; }

    public uint? ScheduleId { get; set; }

    public DateTime HolidayDate { get; set; }

    public string? Description { get; set; }

    [JsonIgnore]
    public virtual Schedule? Schedule { get; set; }
}
