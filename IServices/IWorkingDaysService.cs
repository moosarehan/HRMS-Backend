using HRMS_BACKEND.Dto.WorkingDays;
using HRMS_BACKEND.Entities;

namespace HRMS_BACKEND.IServices;

public interface IWorkingDaysService
{
    Task<WorkingDaysResponseDto> GetByEmployeeIdAsync(int employeeId);
    Task<WorkingDaysResponseDto> UpsertAsync(int employeeId, UpsertWorkingDaysDto dto);
    Task<WorkingDays?> GetScheduleForDateAsync(int employeeId, DateOnly targetDate);
}
