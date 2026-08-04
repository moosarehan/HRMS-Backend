namespace HRMS_BACKEND.Dto.Department;

public class DepartmentResponseDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int EmployeeCount { get; set; }
    public int BranchId { get; set; }
    public string BranchName { get; set; } = string.Empty;
}
