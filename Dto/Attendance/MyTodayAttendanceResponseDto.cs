namespace HRMS_BACKEND.Dto.Attendance;

public class MyTodayAttendanceResponseDto
{
    public bool HasEverBeenAssigned { get; set; }
    public bool HasShift { get; set; }
    public int? ShiftId { get; set; }
    public string? ShiftName { get; set; }
    public TimeOnly? ShiftStartTime { get; set; }
    public TimeOnly? ShiftEndTime { get; set; }
    public DateTime? ShiftStartDateTime { get; set; }
    public DateTime? ShiftEndDateTime { get; set; }
    public bool IsWorkingDayToday { get; set; }
    public AttendanceResponseDto? TodayAttendance { get; set; }
    public DateTime ServerTime { get; set; }
    
    // Leave status fields
    public bool IsOnLeaveToday { get; set; }
    public string? LeaveType { get; set; }
    public string? LeaveStatus { get; set; }
    
    // Late threshold and arrival tracking
    public int LateThresholdMinutes { get; set; } = 0;
    public DateTime? AllowedLateUntil { get; set; }
    public bool IsLateToday { get; set; } = false;
    public int MinutesLateToday { get; set; } = 0;
}
