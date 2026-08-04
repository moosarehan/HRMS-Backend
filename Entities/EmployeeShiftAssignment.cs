using System.ComponentModel.DataAnnotations;

namespace HRMS_BACKEND.Entities;

/// <summary>
/// Tracks the history of shift assignments for employees.
/// Prevents retroactive conflicts when shifts are reassigned during the day.
/// </summary>
public class EmployeeShiftAssignment
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public int EmployeeId { get; set; }
    public Employee Employee { get; set; } = null!;
    
    // Retained as a historical assignment even when the shift is later deleted.
    public int? ShiftId { get; set; }
    public Shift? Shift { get; set; }
    
    /// <summary>
    /// The date this assignment becomes effective (inclusive).
    /// Format: DateOnly to ensure date-only comparisons.
    /// </summary>
    [Required]
    public DateOnly EffectiveFrom { get; set; }
    
    /// <summary>
    /// When this assignment was created (audit trail).
    /// </summary>
    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// Who created this assignment (Admin ID).
    /// </summary>
    public int? CreatedBy { get; set; }
}
