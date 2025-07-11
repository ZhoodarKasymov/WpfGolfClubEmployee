﻿using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace GolfClubServer.Data.Migrations;

public partial class Worker
{
    public string? FullName { get; set; } = null!;

    public string? JobTitle { get; set; } = null!;

    public long? ChatId { get; set; }

    public int? OrganizationId { get; set; }

    public string? TelegramUsername { get; set; }

    public string? Mobile { get; set; }

    public string? AdditionalMobile { get; set; }

    public string? CardNumber { get; set; }

    public string? PhotoPath { get; set; }

    public int? ZoneId { get; set; }

    public DateTime? DeletedAt { get; set; }

    public DateTime StartWork { get; set; }

    public DateTime? EndWork { get; set; }

    public int Id { get; set; }

    public uint? ScheduleId { get; set; }

    [JsonIgnore]
    public virtual ICollection<Employeehistory> Employeehistories { get; set; } = new List<Employeehistory>();

    [JsonIgnore]
    public virtual ICollection<NotifyHistory> NotifyHistories { get; set; } = new List<NotifyHistory>();

    public virtual Organization? Organization { get; set; }

    public virtual Schedule? Schedule { get; set; }

    public virtual Zone? Zone { get; set; }
}
