using HRMS_BACKEND.Dto.Leave;

namespace HRMS_BACKEND.IServices;

public interface ILeaveService
{
    // LeavePeriod Management (Admin only)
    Task<LeavePeriodResponseDto> CreateLeavePeriodAsync(CreateLeavePeriodDto dto);
    Task<LeavePeriodResponseDto> GetCurrentLeavePeriodAsync();

    // LeaveQuota Management (Admin only)
    Task<LeaveQuotaResponseDto> CreateOrUpdateLeaveQuotaAsync(CreateLeaveQuotaDto dto);
    Task<LeaveQuotaResponseDto> UpdateLeaveQuotaAllocatedDaysAsync(int quotaId, UpdateLeaveQuotaDto dto);
    Task<List<LeaveQuotaResponseDto>> GetEmployeeQuotasForCurrentPeriodAsync(int employeeId);
    Task<List<LeaveQuotaResponseDto>> GetAllQuotasForPeriodAsync(int leavePeriodId);

    // LeaveType (seeded, read-only)
    Task<List<LeaveTypeDto>> GetAllLeaveTypesAsync();

    // Leave Setup Validation (for frontend to check if employee can apply)
    Task<LeaveSetupStatusDto> GetLeaveSetupStatusAsync(int employeeId);

    // Leave Application (Employee, Manager, HR can apply)
    Task<LeaveRequestResponseDto> ApplyForLeaveAsync(int employeeId, CreateLeaveRequestDto dto);
    Task<List<LeaveRequestResponseDto>> GetEmployeePendingLeavesAsync(int employeeId);
    Task<List<LeaveRequestResponseDto>> GetAllPendingLeavesAsync(); // Admin view all pending

    // Leave Approval/Rejection (Admin only)
    Task<LeaveRequestResponseDto> ApproveLeaveRequestAsync(int leaveRequestId);
    Task<LeaveRequestResponseDto> RejectLeaveRequestAsync(int leaveRequestId, RejectLeaveRequestDto dto);

    // Leave History/Reporting
    Task<List<LeaveRequestResponseDto>> GetEmployeeLeaveHistoryAsync(int employeeId);
    Task<List<LeaveRequestResponseDto>> GetAllLeavesAsync(); // Admin view all
}
