using System;
using System.Collections.Generic;
using GolfClubSystem.Data.Migrations;
using GolfClubSystem.Models;
using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql.Scaffolding.Internal;

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

    public virtual DbSet<Efmigrationshistory> Efmigrationshistories { get; set; }

    public virtual DbSet<Employeehistory> Employeehistories { get; set; }

    public virtual DbSet<Holiday> Holidays { get; set; }

    public virtual DbSet<Organization> Organizations { get; set; }

    public virtual DbSet<Schedule> Schedules { get; set; }

    public virtual DbSet<Scheduleday> Scheduledays { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<Worker> Workers { get; set; }

    public virtual DbSet<Zone> Zones { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseMySql("server=localhost;database=golf-club-db;user=root;password=123456", Microsoft.EntityFrameworkCore.ServerVersion.Parse("8.0.37-mysql"));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder
            .UseCollation("utf8mb4_0900_ai_ci")
            .HasCharSet("utf8mb4");

        modelBuilder.Entity<Efmigrationshistory>(entity =>
        {
            entity.HasKey(e => e.MigrationId).HasName("PRIMARY");

            entity.ToTable("__efmigrationshistory");

            entity.Property(e => e.MigrationId).HasMaxLength(150);
            entity.Property(e => e.ProductVersion).HasMaxLength(32);
        });

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

            entity.HasOne(d => d.Schedule).WithMany(p => p.Holidays)
                .HasForeignKey(d => d.ScheduleId)
                .HasConstraintName("FK_Holiday_Schedule_Id");
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
            entity.Property(e => e.PermissibleEarlyLeaveEnd)
                .HasDefaultValueSql("'00:30:00'")
                .HasColumnType("time");
            entity.Property(e => e.PermissibleEarlyLeaveStart)
                .HasDefaultValueSql("'00:30:00'")
                .HasColumnType("time");
            entity.Property(e => e.PermissibleLateTimeEnd)
                .HasDefaultValueSql("'00:30:00'")
                .HasColumnType("time");
            entity.Property(e => e.PermissibleLateTimeStart)
                .HasDefaultValueSql("'00:30:00'")
                .HasColumnType("time");
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
            entity.Property(e => e.Password).HasMaxLength(255);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
