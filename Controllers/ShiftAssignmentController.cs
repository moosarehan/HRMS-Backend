using System.Security.Claims;
using HRMS_BACKEND.GenericResponse;
using HRMS_BACKEND.IServices;
using HRMS_BACKEND.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS_BACKEND.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ShiftAssignmentController : ControllerBase
{
    private readonly IShiftAssignmentService _shiftAssignmentService;

    public ShiftAssignmentController(IShiftAssignmentService shiftAssignmentService)
    {
        _shiftAssignmentService = shiftAssignmentService;
    }

    private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    /// <summary>
    /// Assigns a shift to an employee effective from tomorrow (prevents retroactive conflicts).
    /// This is the recommended method for admin shift reassignments.
    /// </summary>
    [HttpPost("assign-for-tomorrow")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AssignShiftForTomorrow([FromBody] AssignShiftRequest request)
    {
        try
        {
            var assignment = await _shiftAssignmentService.ReassignShiftForTomorrowAsync(
                request.EmployeeId, 
                request.ShiftId, 
                CurrentUserId
            );

            return Ok(ApiResponse<object>.SuccessResponse(new
            {
                assignment.Id,
                assignment.EmployeeId,
                EmployeeName = assignment.Employee.FullName,
                assignment.ShiftId,
                ShiftName = assignment.Shift.Name,
                assignment.EffectiveFrom,
                assignment.CreatedAt
            }, "Shift assignment will take effect tomorrow."));
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

    /// <summary>
    /// Gets the effective shift for an employee on a specific date.
    /// </summary>
    [HttpGet("effective-shift/{employeeId}")]
    [Authorize(Roles = "Admin,HR,Manager")]
    public async Task<IActionResult> GetEffectiveShift(int employeeId, [FromQuery] DateOnly? date)
    {
        try
        {
            var targetDate = date ?? DateOnly.FromDateTime(DateTime.Now);
            var shift = await _shiftAssignmentService.GetEffectiveShiftForDateAsync(employeeId, targetDate);

            if (shift == null)
            {
                return Ok(ApiResponse<object>.SuccessResponse(null, $"No shift assigned for employee {employeeId} on {targetDate:yyyy-MM-dd}"));
            }

            return Ok(ApiResponse<object>.SuccessResponse(new
            {
                shift.Id,
                shift.Name,
                shift.StartTime,
                shift.EndTime,
                shift.Limit,
                EffectiveDate = targetDate
            }));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<string>.FailResponse($"Error retrieving effective shift: {ex.Message}"));
        }
    }

    /// <summary>
    /// Gets the shift assignment history for an employee.
    /// </summary>
    [HttpGet("history/{employeeId}")]
    [Authorize(Roles = "Admin,HR")]
    public async Task<IActionResult> GetAssignmentHistory(int employeeId)
    {
        try
        {
            var history = await _shiftAssignmentService.GetAssignmentHistoryAsync(employeeId);

            var response = history.Select(a => new
            {
                a.Id,
                a.EmployeeId,
                a.ShiftId,
                ShiftName = a.Shift.Name,
                a.EffectiveFrom,
                a.CreatedAt,
                a.CreatedBy
            }).ToList();

            return Ok(ApiResponse<object>.SuccessResponse(response));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<string>.FailResponse($"Error retrieving assignment history: {ex.Message}"));
        }
    }

    /// <summary>
    /// One-time migration endpoint to move existing Employee.ShiftId values to the new system.
    /// Should only be called once after deploying the new shift assignment system.
    /// </summary>
    [HttpPost("migrate-existing-assignments")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> MigrateExistingAssignments([FromServices] ShiftMigrationService migrationService)
    {
        try
        {
            await migrationService.MigrateExistingShiftAssignmentsAsync();
            return Ok(ApiResponse<string>.SuccessResponse("Migration completed successfully."));
        }
        catch (Exception ex)
        {
            return StatusCode(500, ApiResponse<string>.FailResponse($"Migration failed: {ex.Message}"));
        }
    }
}

public class AssignShiftRequest
{
    public int EmployeeId { get; set; }
    public int ShiftId { get; set; }
}