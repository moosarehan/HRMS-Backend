namespace HRMS_BACKEND.Dto.Employee;

public class EmployeeResponseDto
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Designation { get; set; }
    public decimal Salary { get; set; }
    public int? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public int? BranchId { get; set; }
    public string? BranchName { get; set; }
    public int? ShiftId { get; set; }
    public string? ShiftName { get; set; }
    public int? PendingShiftId { get; set; }
    public string? PendingShiftName { get; set; }
    public DateOnly? PendingShiftEffectiveFromDate { get; set; }
    public int? ManagerId { get; set; }
    public string? ManagerName { get; set; }
    public bool IsActive { get; set; }
    
    // Attendance statistics - Late and Absent counts from first assignment
    public int LateCount { get; set; } = 0; // Total late clock-ins from first assignment
    public int AbsentCount { get; set; } = 0; // Total absences from first assignment
}
