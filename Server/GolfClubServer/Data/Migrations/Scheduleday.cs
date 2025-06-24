using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace GolfClubServer.Data.Migrations;

public partial class Scheduleday
{
    public uint Id { get; set; }

    public uint? ScheduleId { get; set; }

    public string DayOfWeek { get; set; } = null!;

    public TimeOnly? WorkStart { get; set; }

    public TimeOnly? WorkEnd { get; set; }

    public bool IsSelected { get; set; }

    [JsonIgnore]
    public virtual Schedule? Schedule { get; set; }
}
