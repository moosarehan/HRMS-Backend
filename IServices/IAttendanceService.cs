using HRMS_BACKEND.Dto.Attendance;

namespace HRMS_BACKEND.IServices;

public interface IAttendanceService
{
    Task<AttendanceResponseDto> ClockInAsync(int employeeId);
    Task<AttendanceResponseDto> ClockOutAsync(int employeeId, ClockOutRequestDto dto);
    Task<AttendanceResponseDto> ApproveEmergencyClockOutAsync(int attendanceId);
    Task<AttendanceResponseDto> RejectEmergencyClockOutAsync(int attendanceId);
    Task<AdminTimesheetResponseDto> GetAdminTimesheetAsync(DateOnly? date);
    Task<MyTodayAttendanceResponseDto> GetMyTodayAttendanceAsync(int employeeId);
    Task<List<AttendanceResponseDto>> GetMyAttendanceHistoryAsync(int employeeId, DateOnly? startDate, DateOnly? endDate);
    Task<List<AttendanceExportDto>> GetFilteredForExportAsync(string? branch, string? department, string? search, DateOnly? date, string? status = null);
    Task<Dictionary<int, object>> GetAttendanceStatsAsync(DateOnly? date);
    Task<int> BackfillShiftAssignmentsAsync();
}
