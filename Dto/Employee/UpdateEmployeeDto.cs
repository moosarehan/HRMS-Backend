using HRMS_BACKEND.Entities.Enums;

namespace HRMS_BACKEND.Dto.Employee;

public class UpdateEmployeeDto
{
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? Designation { get; set; }
    public decimal Salary { get; set; }
    public Role Role { get; set; } = Role.Employee;  // Default to preserve role if not provided
    public int? DepartmentId { get; set; }
    public int? ManagerId { get; set; }
    public bool IsActive { get; set; } = true;
}
