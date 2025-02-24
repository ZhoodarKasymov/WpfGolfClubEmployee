using System.Windows;
using GolfClubSystem.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace GolfClubSystem.Context;

public partial class MyDbContext : DbContext
{
    public MyDbContext()
    {
    }

    public MyDbContext(DbContextOptions<MyDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Employeehistory> Employeehistories { get; set; }

    public virtual DbSet<Holiday> Holidays { get; set; }

    public virtual DbSet<NotifyHistory> NotifyHistories { get; set; }

    public virtual DbSet<NotifyJob> NotifyJobs { get; set; }

    public virtual DbSet<Organization> Organizations { get; set; }

    public virtual DbSet<Schedule> Schedules { get; set; }

    public virtual DbSet<Scheduleday> Scheduledays { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<Worker> Workers { get; set; }

    public virtual DbSet<Zone> Zones { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var configuration = ((App)Application.Current)._configuration;
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        optionsBuilder.UseMySql(connectionString, ServerVersion.Parse("8.0.37-mysql"));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasCharSet("utf8mb4");

        modelBuilder.Entity<Employeehistory>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("employeehistory");

            entity.HasIndex(e => e.WorkerId, "WorkerId");

            entity.HasIndex(e => e.MarkZoneId, "employeehistory_MarkZoneId");

            entity.Property(e => e.ArrivalTime).HasColumnType("datetime");
            entity.Property(e => e.LeaveTime).HasColumnType("datetime");
            entity.Property(e => e.MarkTime).HasColumnType("datetime");

            entity.HasOne(d => d.MarkZone).WithMany(p => p.Employeehistories)
                .HasForeignKey(d => d.MarkZoneId)
                .HasConstraintName("employeehistory_MarkZoneId");

            entity.HasOne(d => d.Worker).WithMany(p => p.Employeehistories)
                .HasForeignKey(d => d.WorkerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("employeehistory_ibfk_1");
        });

        modelBuilder.Entity<Holiday>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("holiday");

            entity.HasIndex(e => e.ScheduleId, "FK_Holiday_Schedule_Id");

            entity.HasIndex(e => e.Id, "Id").IsUnique();

            entity.Property(e => e.Description).HasMaxLength(255);
            entity.Property(e => e.HolidayDate).HasColumnType("datetime");

            entity.HasOne(d => d.Schedule).WithMany(p => p.Holidays)
                .HasForeignKey(d => d.ScheduleId)
                .HasConstraintName("FK_Holiday_Schedule_Id");
        });

        modelBuilder.Entity<NotifyHistory>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("notify_history");

            entity.HasIndex(e => e.WorkerId, "notify_history_ibfk_1");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ArrivalTime)
                .HasColumnType("datetime")
                .HasColumnName("arrival_time");
            entity.Property(e => e.MarkTime)
                .HasColumnType("datetime")
                .HasColumnName("mark_time");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.WorkerId).HasColumnName("worker_id");

            entity.HasOne(d => d.Worker).WithMany(p => p.NotifyHistories)
                .HasForeignKey(d => d.WorkerId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("notify_history_ibfk_1");
        });

        modelBuilder.Entity<NotifyJob>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("notify_jobs");

            entity.HasIndex(e => e.ShiftId, "notify_jobs_ShiftId");

            entity.HasIndex(e => e.ZoneId, "notify_jobs_ZoneId");

            entity.HasIndex(e => e.OrganizationId, "notify_jobs_org_id_ibfk_1");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Message)
                .HasMaxLength(255)
                .HasColumnName("message");
            entity.Property(e => e.OrganizationId).HasColumnName("organization_id");
            entity.Property(e => e.Percentage)
                .HasPrecision(5, 2)
                .HasColumnName("percentage");
            entity.Property(e => e.ShiftId).HasColumnName("shift_id");
            entity.Property(e => e.Time)
                .HasColumnType("time")
                .HasColumnName("time");
            entity.Property(e => e.WorkerIds)
                .HasColumnType("json")
                .HasColumnName("worker_ids");
            entity.Property(e => e.ZoneId).HasColumnName("zone_id");

            entity.HasOne(d => d.Organization).WithMany(p => p.NotifyJobs)
                .HasForeignKey(d => d.OrganizationId)
                .HasConstraintName("notify_jobs_org_id_ibfk_1");

            entity.HasOne(d => d.Shift).WithMany(p => p.NotifyJobs)
                .HasForeignKey(d => d.ShiftId)
                .HasConstraintName("notify_jobs_ShiftId");

            entity.HasOne(d => d.Zone).WithMany(p => p.NotifyJobs)
                .HasForeignKey(d => d.ZoneId)
                .HasConstraintName("notify_jobs_ZoneId");
        });

        modelBuilder.Entity<Organization>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("organization");

            entity.HasIndex(e => e.ParentOrganizationId, "IX_Organization_ParentOrganizationId");

            entity.Property(e => e.DeletedAt).HasColumnType("timestamp");

            entity.HasOne(d => d.ParentOrganization).WithMany(p => p.InverseParentOrganization)
                .HasForeignKey(d => d.ParentOrganizationId)
                .HasConstraintName("FK_Organization_Organization_ParentOrganizationId");
        });

        modelBuilder.Entity<Schedule>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("schedule");

            entity.HasIndex(e => e.Id, "Id").IsUnique();

            entity.Property(e => e.BreakEnd).HasColumnType("time");
            entity.Property(e => e.BreakStart).HasColumnType("time");
            entity.Property(e => e.DeletedAt).HasColumnType("datetime");
            entity.Property(e => e.Name).HasMaxLength(255);
            entity.Property(e => e.PermissibleEarlyLeaveEnd).HasColumnType("time");
            entity.Property(e => e.PermissibleEarlyLeaveStart).HasColumnType("time");
            entity.Property(e => e.PermissibleLateTimeEnd).HasColumnType("time");
            entity.Property(e => e.PermissibleLateTimeStart).HasColumnType("time");
            entity.Property(e => e.PermissionToLateTime).HasColumnType("time");
        });

        modelBuilder.Entity<Scheduleday>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("scheduleday");

            entity.HasIndex(e => e.ScheduleId, "FK_ScheduleDay_Schedule_Id");

            entity.HasIndex(e => e.Id, "Id").IsUnique();

            entity.Property(e => e.DayOfWeek).HasMaxLength(50);
            entity.Property(e => e.WorkEnd).HasColumnType("time");
            entity.Property(e => e.WorkStart).HasColumnType("time");

            entity.HasOne(d => d.Schedule).WithMany(p => p.Scheduledays)
                .HasForeignKey(d => d.ScheduleId)
                .HasConstraintName("FK_ScheduleDay_Schedule_Id");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity
                .HasNoKey()
                .ToTable("users");
        });

        modelBuilder.Entity<Worker>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("workers");

            entity.HasIndex(e => e.ZoneId, "FK_Workers_Organization_ZoneId");

            entity.HasIndex(e => e.ScheduleId, "FK_Workers_ScheduleId");

            entity.HasIndex(e => e.OrganizationId, "IX_Workers_OrganizationId");

            entity.Property(e => e.AdditionalMobile).HasMaxLength(255);
            entity.Property(e => e.CardNumber).HasMaxLength(255);
            entity.Property(e => e.DeletedAt).HasColumnType("timestamp");
            entity.Property(e => e.EndWork).HasColumnType("timestamp");
            entity.Property(e => e.Mobile).HasMaxLength(255);
            entity.Property(e => e.PhotoPath).HasMaxLength(255);
            entity.Property(e => e.StartWork).HasColumnType("timestamp");

            entity.HasOne(d => d.Organization).WithMany(p => p.Workers)
                .HasForeignKey(d => d.OrganizationId)
                .HasConstraintName("FK_Workers_Organization_OrganizationId");

            entity.HasOne(d => d.Schedule).WithMany(p => p.Workers)
                .HasForeignKey(d => d.ScheduleId)
                .HasConstraintName("FK_Workers_Schedule_Id");

            entity.HasOne(d => d.Zone).WithMany(p => p.Workers)
                .HasForeignKey(d => d.ZoneId)
                .HasConstraintName("FK_Workers_Organization_ZoneId");
        });

        modelBuilder.Entity<Zone>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PRIMARY");

            entity.ToTable("zones");

            entity.Property(e => e.DeletedAt).HasColumnType("timestamp");
            entity.Property(e => e.EnterIp).HasMaxLength(255);
            entity.Property(e => e.ExitIp).HasMaxLength(255);
            entity.Property(e => e.Login).HasMaxLength(255);
            entity.Property(e => e.Name).HasMaxLength(255);
            entity.Property(e => e.NotifyIp).HasMaxLength(255);
            entity.Property(e => e.Password).HasMaxLength(255);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
