namespace HRMS_BACKEND.Dto.Attendance;

public class AdminTimesheetResponseDto
{
    public DateOnly Date { get; set; }
    public bool IsCompanyOffDay { get; set; }
    // True when the selected date is before every employee's first assignment.
    public bool NoShiftAssignmentsYet { get; set; }
    public List<TimesheetEmployeeDto> PresentEmployees { get; set; } = new();
    public List<TimesheetEmployeeDto> LateEmployees { get; set; } = new(); // Clocked in after shift start + grace period
    public List<TimesheetEmployeeDto> AbsentEmployees { get; set; } = new();
    public List<TimesheetEmployeeDto> HolidayEmployees { get; set; } = new(); // On leave or scheduled off
    public List<PendingEmergencyClockOutDto> PendingEmergencyClockOutRequests { get; set; } = new();
}
