using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using HRMS_BACKEND.Entities.Enums;

namespace HRMS_BACKEND.Entities;

public class LeaveMaster
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int EmployeeId { get; set; }

    [ForeignKey(nameof(EmployeeId))]
    public Employee? Employee { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }

    [Required]
    public int LeaveTypeId { get; set; }

    [ForeignKey(nameof(LeaveTypeId))]
    public LeaveType? LeaveType { get; set; }

    [Required]
    public int LeavePeriodId { get; set; }

    [ForeignKey(nameof(LeavePeriodId))]
    public LeavePeriod? LeavePeriod { get; set; }

    [Required]
    public int NoOfDays { get; set; }

    [Required]
    public LeaveStatus Status { get; set; } = LeaveStatus.Pending;

    public string? Description { get; set; }

    public string? RejectionReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
