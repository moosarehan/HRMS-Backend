using System.Security.Claims;
using HRMS_BACKEND.Dto.Attendance;
using HRMS_BACKEND.GenericResponse;
using HRMS_BACKEND.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS_BACKEND.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AttendanceController : ControllerBase
{
    private readonly IAttendanceService _service;
    private readonly IExportService _exportService;

    public AttendanceController(IAttendanceService service, IExportService exportService) 
    {
        _service = service;
        _exportService = exportService;
    }

    private int CurrentEmployeeId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet("my-today")]
    [Authorize(Roles = "Employee,Manager,HR")]
    public async Task<IActionResult> GetMyToday()
    {
        try
        {
            var result = await _service.GetMyTodayAttendanceAsync(CurrentEmployeeId);
            return Ok(ApiResponse<MyTodayAttendanceResponseDto>.SuccessResponse(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.FailResponse(ex.Message));
        }
    }

    [HttpGet("my-history")]
    [Authorize(Roles = "Employee,Manager,HR")]
    public async Task<IActionResult> GetMyHistory([FromQuery] DateOnly? startDate, [FromQuery] DateOnly? endDate, [FromQuery] string? search)
    {
        var result = await _service.GetMyAttendanceHistoryAsync(CurrentEmployeeId, startDate, endDate);
        return Ok(ApiResponse<List<AttendanceResponseDto>>.SuccessResponse(result));
    }

    [HttpPost("clock-in")]
    [Authorize(Roles = "Employee,Manager,HR")]
    public async Task<IActionResult> ClockIn()
    {
        try
        {
            var result = await _service.ClockInAsync(CurrentEmployeeId);
            return Ok(ApiResponse<AttendanceResponseDto>.SuccessResponse(result, "Clock-in recorded."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.FailResponse(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<string>.FailResponse(ex.Message));
        }
    }

    [HttpPost("clock-out")]
    [Authorize(Roles = "Employee,Manager,HR")]
    public async Task<IActionResult> ClockOut(ClockOutRequestDto dto)
    {
        try
        {
            var result = await _service.ClockOutAsync(CurrentEmployeeId, dto);
            var message = result.ClockOut.HasValue
                ? "Clock-out recorded."
                : "Emergency clock-out request submitted for admin approval.";

            return Ok(ApiResponse<AttendanceResponseDto>.SuccessResponse(result, message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<string>.FailResponse(ex.Message));
        }
    }

    [HttpGet("admin/timesheet")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAdminTimesheet([FromQuery] DateOnly? date)
    {
        var result = await _service.GetAdminTimesheetAsync(date);
        return Ok(ApiResponse<AdminTimesheetResponseDto>.SuccessResponse(result));
    }

    [HttpGet("stats")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAttendanceStats()
    {
        var stats = await _service.GetAttendanceStatsAsync(null);
        return Ok(ApiResponse<Dictionary<int, object>>.SuccessResponse(stats));
    }

    [HttpPost("admin/backfill-shift-assignments")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> BackfillShiftAssignments()
    {
        var backfilledCount = await _service.BackfillShiftAssignmentsAsync();
        return Ok(ApiResponse<object>.SuccessResponse(
            new { BackfilledCount = backfilledCount }, 
            $"Backfilled {backfilledCount} shift assignments successfully."
        ));
    }

    [HttpPut("admin/emergency-clock-out/{attendanceId}/approve")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ApproveEmergencyClockOut(int attendanceId)
    {
        try
        {
            var result = await _service.ApproveEmergencyClockOutAsync(attendanceId);
            return Ok(ApiResponse<AttendanceResponseDto>.SuccessResponse(result, "Emergency clock-out request approved."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.FailResponse(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<string>.FailResponse(ex.Message));
        }
    }

    [HttpPut("admin/emergency-clock-out/{attendanceId}/reject")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RejectEmergencyClockOut(int attendanceId)
    {
        try
        {
            var result = await _service.RejectEmergencyClockOutAsync(attendanceId);
            return Ok(ApiResponse<AttendanceResponseDto>.SuccessResponse(result, "Emergency clock-out request rejected."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.FailResponse(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<string>.FailResponse(ex.Message));
        }
    }

    [HttpGet("export")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ExportAttendance(
        [FromQuery] string format,
        [FromQuery] string? branch,
        [FromQuery] string? department,
        [FromQuery] string? search,
        [FromQuery] DateOnly? date,
        [FromQuery] string? status)
    {
        try
        {
            var records = await _service.GetFilteredForExportAsync(branch, department, search, date, status);

            return format?.ToLower() switch
            {
                "txt" => File(_exportService.ToTxt(records), "text/plain", $"attendance_{DateTime.Now:yyyyMMdd}.txt"),
                "pdf" => File(_exportService.ToPdf(records), "application/pdf", $"attendance_{DateTime.Now:yyyyMMdd}.pdf"),
                "docx" => File(_exportService.ToDocx(records),
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                    $"attendance_{DateTime.Now:yyyyMMdd}.docx"),
                _ => BadRequest(ApiResponse<string>.FailResponse("Invalid format. Use pdf, docx, or txt."))
            };
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<string>.FailResponse($"Export failed: {ex.Message}"));
        }
    }
}
