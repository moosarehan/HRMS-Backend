namespace HRMS_BACKEND.Dto.Branch;

public class BranchDeleteImpactDto
{
    public string BranchName { get; set; } = string.Empty;
    public int DepartmentCount { get; set; }
    public int EmployeeCount { get; set; }
}
