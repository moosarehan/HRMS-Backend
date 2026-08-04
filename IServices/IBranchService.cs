using HRMS_BACKEND.Dto.Branch;
using HRMS_BACKEND.Dto.Department;
using HRMS_BACKEND.Entities.Enums;

namespace HRMS_BACKEND.IServices;

public interface IBranchService
{
    Task<List<BranchResponseDto>> GetAllAsync(Role currentRole, int currentId);
    Task<BranchResponseDto> GetByIdAsync(int id);
    Task<List<DepartmentResponseDto>> GetDepartmentsByBranchAsync(int branchId);
    Task<BranchResponseDto> CreateAsync(CreateBranchDto dto);
    Task<BranchResponseDto> UpdateAsync(int id, UpdateBranchDto dto);
    Task<BranchDeleteImpactDto> GetDeleteImpactAsync(int id);
    Task DeleteAsync(int id);
}
