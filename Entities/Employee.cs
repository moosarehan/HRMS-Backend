using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HRMS_BACKEND.Entities.Enums;

namespace HRMS_BACKEND.Entities;

public class Employee
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string FullName { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    public Role Role { get; set; }

    public string? Phone { get; set; }
    public string? Address { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public DateTime JoiningDate { get; set; } = DateTime.UtcNow;
    public string? Designation { get; set; }

    [Column(TypeName = "decimal(12,2)")]
    public decimal Salary { get; set; }

    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }

    public int? BranchId { get; set; }
    public Branch? Branch { get; set; }

    public int? ShiftId { get; set; }
    public Shift? Shift { get; set; }

    /// <summary>
    /// The effective date of the employee's first-ever shift assignment. This is
    /// immutable once set so absence tracking survives shift changes and deletions.
    /// </summary>
    public DateOnly? FirstShiftAssignmentDate { get; set; }

    public int? PendingShiftId { get; set; }
    public Shift? PendingShift { get; set; }

    public DateOnly? PendingShiftEffectiveFromDate { get; set; }

    public int? ManagerId { get; set; }
    public Employee? Manager { get; set; }
    public ICollection<Employee> DirectReports { get; set; } = new List<Employee>();

    public ICollection<WorkingDays> WorkingDaysHistory { get; set; } = new List<WorkingDays>();
    public ICollection<EmployeeAttendance> Attendances { get; set; } = new List<EmployeeAttendance>();

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
