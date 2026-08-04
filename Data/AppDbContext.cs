using HRMS_BACKEND.Entities;
using Microsoft.EntityFrameworkCore;

namespace HRMS_BACKEND.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Branch> Branches { get; set; }
    public DbSet<Employee> Employees { get; set; }
    public DbSet<Department> Departments { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }
    public DbSet<LeaveType> LeaveTypes { get; set; }
    public DbSet<LeavePeriod> LeavePeriods { get; set; }
    public DbSet<LeaveQuota> LeaveQuotas { get; set; }
    public DbSet<LeaveMaster> LeaveMasters { get; set; }
    public DbSet<Shift> Shifts { get; set; }
    public DbSet<WorkingDays> WorkingDays { get; set; }
    public DbSet<EmployeeAttendance> EmployeeAttendances { get; set; }
    public DbSet<EmployeeShiftAssignment> EmployeeShiftAssignments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Branch.Name unique index
        modelBuilder.Entity<Branch>()
            .HasIndex(b => b.Name)
            .IsUnique();

        // Department -> Branch (required FK)
        modelBuilder.Entity<Department>()
            .HasOne(d => d.Branch)
            .WithMany(b => b.Departments)
            .HasForeignKey(d => d.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        // Employee -> Branch (optional FK for now, resolved from department)
        modelBuilder.Entity<Employee>()
            .HasOne(e => e.Branch)
            .WithMany()
            .HasForeignKey(e => e.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        // Email must be unique — used for login
        modelBuilder.Entity<Employee>()
            .HasIndex(e => e.Email)
            .IsUnique();

        // Shift names are fixed and unique.
        modelBuilder.Entity<Shift>()
            .HasIndex(s => s.Name)
            .IsUnique();

        // Self-referencing relationship: Employee -> Manager
        modelBuilder.Entity<Employee>()
            .HasOne(e => e.Manager)
            .WithMany(e => e.DirectReports)
            .HasForeignKey(e => e.ManagerId)
            .OnDelete(DeleteBehavior.Restrict);

        // Employee -> Department (many employees to one department)
        modelBuilder.Entity<Employee>()
            .HasOne(e => e.Department)
            .WithMany(d => d.Employees)
            .HasForeignKey(e => e.DepartmentId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Employee>()
            .HasOne(e => e.Shift)
            .WithMany(s => s.Employees)
            .HasForeignKey(e => e.ShiftId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Employee>()
            .HasOne(e => e.PendingShift)
            .WithMany()
            .HasForeignKey(e => e.PendingShiftId)
            .OnDelete(DeleteBehavior.Restrict);

        // EmployeeShiftAssignment relationships
        modelBuilder.Entity<EmployeeShiftAssignment>(entity =>
        {
            entity.HasKey(e => e.Id);
            
            entity.HasOne(e => e.Employee)
                .WithMany()
                .HasForeignKey(e => e.EmployeeId)
                .OnDelete(DeleteBehavior.Cascade);
                
            entity.HasOne(e => e.Shift)
                .WithMany()
                .HasForeignKey(e => e.ShiftId)
                .OnDelete(DeleteBehavior.SetNull);
                
            // Index for efficient queries by employee and date
            entity.HasIndex(e => new { e.EmployeeId, e.EffectiveFrom })
                .HasDatabaseName("IX_EmployeeShiftAssignments_Employee_Date");
        });

        modelBuilder.Entity<WorkingDays>()
            .HasKey(wd => wd.Id);

        modelBuilder.Entity<WorkingDays>()
            .HasIndex(wd => new { wd.EmployeeId, wd.EffectiveFromDate })
            .IsUnique()
            .HasDatabaseName("UX_WorkingDays_Employee_EffectiveFrom");

        modelBuilder.Entity<WorkingDays>()
            .HasOne(wd => wd.Employee)
            .WithMany(e => e.WorkingDaysHistory)
            .HasForeignKey(wd => wd.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<EmployeeAttendance>()
            .HasOne(a => a.Employee)
            .WithMany(e => e.Attendances)
            .HasForeignKey(a => a.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<EmployeeAttendance>()
            .HasOne(a => a.Shift)
            .WithMany(s => s.Attendances)
            .HasForeignKey(a => a.ShiftId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<EmployeeAttendance>()
            .HasIndex(a => new { a.EmployeeId, a.Date })
            .IsUnique()
            .HasDatabaseName("UX_EmployeeAttendance_Employee_Date");

        // RefreshToken -> Employee
        modelBuilder.Entity<RefreshToken>()
            .HasOne(rt => rt.Employee)
            .WithMany()
            .HasForeignKey(rt => rt.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        // LeaveType relationships
        modelBuilder.Entity<LeaveType>()
            .HasMany(lt => lt.LeaveQuotas)
            .WithOne(lq => lq.LeaveType)
            .HasForeignKey(lq => lq.LeaveTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<LeaveType>()
            .HasMany(lt => lt.LeaveMasters)
            .WithOne(lm => lm.LeaveType)
            .HasForeignKey(lm => lm.LeaveTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        // LeavePeriod relationships
        modelBuilder.Entity<LeavePeriod>()
            .HasMany(lp => lp.LeaveQuotas)
            .WithOne(lq => lq.LeavePeriod)
            .HasForeignKey(lq => lq.LeavePeriodId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<LeavePeriod>()
            .HasMany(lp => lp.LeaveMasters)
            .WithOne(lm => lm.LeavePeriod)
            .HasForeignKey(lm => lm.LeavePeriodId)
            .OnDelete(DeleteBehavior.Restrict);

        // LeaveQuota relationships
        modelBuilder.Entity<LeaveQuota>()
            .HasOne(lq => lq.Employee)
            .WithMany()
            .HasForeignKey(lq => lq.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        // Composite unique index on LeaveQuota to ensure one row per employee/type/period
        modelBuilder.Entity<LeaveQuota>()
            .HasIndex(lq => new { lq.EmployeeId, lq.LeaveTypeId, lq.LeavePeriodId })
            .IsUnique()
            .HasDatabaseName("UX_LeaveQuota_EmployeeTypePerious");

        // LeaveMaster relationships
        modelBuilder.Entity<LeaveMaster>()
            .HasOne(lm => lm.Employee)
            .WithMany()
            .HasForeignKey(lm => lm.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Shift>().HasData(
            new Shift
            {
                Id = 1,
                Name = "Morning Shift",
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(17, 0),
                Limit = 100
            },
            new Shift
            {
                Id = 2,
                Name = "Night Shift",
                StartTime = new TimeOnly(20, 0),
                EndTime = new TimeOnly(4, 0),
                Limit = 100
            },
            new Shift
            {
                Id = 3,
                Name = "Remote Shift",
                StartTime = new TimeOnly(9, 0),
                EndTime = new TimeOnly(17, 0),
                Limit = 100
            });
    }
}
