using HRMS_BACKEND.Dto.Leave;
using HRMS_BACKEND.GenericResponse;
using HRMS_BACKEND.IServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HRMS_BACKEND.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LeaveController : ControllerBase
{
    private readonly ILeaveService _service;
    public LeaveController(ILeaveService service) => _service = service;

    [HttpPost("periods")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateLeavePeriod(CreateLeavePeriodDto dto)
    {
        try
        {
            var result = await _service.CreateLeavePeriodAsync(dto);
            return Ok(ApiResponse<LeavePeriodResponseDto>.SuccessResponse(result, "Leave period created successfully."));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<string>.FailResponse(ex.Message));
        }
    }

    [HttpGet("periods/current")]
    public async Task<IActionResult> GetCurrentLeavePeriod()
    {
        try
        {
            var result = await _service.GetCurrentLeavePeriodAsync();
            return Ok(ApiResponse<LeavePeriodResponseDto>.SuccessResponse(result));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<string>.FailResponse(ex.Message));
        }
    }

    // ===== LeaveQuota Endpoints (Admin only) =====
    [HttpPost("quotas")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateOrUpdateLeaveQuota(CreateLeaveQuotaDto dto)
    {
        try
        {
            var result = await _service.CreateOrUpdateLeaveQuotaAsync(dto);
            return Ok(ApiResponse<LeaveQuotaResponseDto>.SuccessResponse(result, "Leave quota created/updated successfully."));
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

    [HttpPut("quotas/{quotaId}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateLeaveQuotaAllocatedDays(int quotaId, UpdateLeaveQuotaDto dto)
    {
        try
        {
            var result = await _service.UpdateLeaveQuotaAllocatedDaysAsync(quotaId, dto);
            return Ok(ApiResponse<LeaveQuotaResponseDto>.SuccessResponse(result, "Leave quota updated successfully."));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.FailResponse(ex.Message));
        }
    }

    [HttpGet("quotas/employee/{employeeId}/current")]
    public async Task<IActionResult> GetEmployeeQuotasForCurrentPeriod(int employeeId)
    {
        try
        {
            var result = await _service.GetEmployeeQuotasForCurrentPeriodAsync(employeeId);
            return Ok(ApiResponse<List<LeaveQuotaResponseDto>>.SuccessResponse(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.FailResponse(ex.Message));
        }
    }

    [HttpGet("quotas/period/{leavePeriodId}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAllQuotasForPeriod(int leavePeriodId)
    {
        try
        {
            var result = await _service.GetAllQuotasForPeriodAsync(leavePeriodId);
            return Ok(ApiResponse<List<LeaveQuotaResponseDto>>.SuccessResponse(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.FailResponse(ex.Message));
        }
    }

    // ===== LeaveType Endpoints (Read-only) =====
    [HttpGet("types")]
    public async Task<IActionResult> GetAllLeaveTypes()
    {
        var result = await _service.GetAllLeaveTypesAsync();
        return Ok(ApiResponse<List<LeaveTypeDto>>.SuccessResponse(result));
    }

    // ===== Leave Setup Validation Endpoint =====
    [HttpGet("setup-status/employee/{employeeId}")]
    public async Task<IActionResult> GetLeaveSetupStatus(int employeeId)
    {
        try
        {
            var result = await _service.GetLeaveSetupStatusAsync(employeeId);
            return Ok(ApiResponse<LeaveSetupStatusDto>.SuccessResponse(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.FailResponse(ex.Message));
        }
    }

    // ===== Leave Application Endpoints (Employee, Manager, HR) =====
    [HttpPost("requests/apply")]
    [Authorize(Roles = "Employee,Manager,HR")]
    public async Task<IActionResult> ApplyForLeave(CreateLeaveRequestDto dto)
    {
        try
        {
            // Extract employee ID from the current user's claims
            var employeeIdClaim = User.FindFirst("employeeId")?.Value;
            if (string.IsNullOrEmpty(employeeIdClaim) || !int.TryParse(employeeIdClaim, out var employeeId))
            {
                return Unauthorized(ApiResponse<string>.FailResponse("Unable to identify current employee."));
            }

            var result = await _service.ApplyForLeaveAsync(employeeId, dto);
            return Ok(ApiResponse<LeaveRequestResponseDto>.SuccessResponse(result, "Leave request submitted successfully."));
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

    [HttpGet("requests/pending")]
    [Authorize(Roles = "Employee,Manager,HR")]
    public async Task<IActionResult> GetEmployeePendingLeaves()
    {
        try
        {
            var employeeIdClaim = User.FindFirst("employeeId")?.Value;
            if (string.IsNullOrEmpty(employeeIdClaim) || !int.TryParse(employeeIdClaim, out var employeeId))
            {
                return Unauthorized(ApiResponse<string>.FailResponse("Unable to identify current employee."));
            }

            var result = await _service.GetEmployeePendingLeavesAsync(employeeId);
            return Ok(ApiResponse<List<LeaveRequestResponseDto>>.SuccessResponse(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.FailResponse(ex.Message));
        }
    }

    [HttpGet("requests/pending/all")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAllPendingLeaves()
    {
        var result = await _service.GetAllPendingLeavesAsync();
        return Ok(ApiResponse<List<LeaveRequestResponseDto>>.SuccessResponse(result));
    }

    // ===== Leave Approval/Rejection Endpoints (Admin only) =====
    [HttpPut("requests/{leaveRequestId}/approve")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ApproveLeaveRequest(int leaveRequestId)
    {
        try
        {
            var result = await _service.ApproveLeaveRequestAsync(leaveRequestId);
            return Ok(ApiResponse<LeaveRequestResponseDto>.SuccessResponse(result, "Leave request approved successfully."));
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

    [HttpPut("requests/{leaveRequestId}/reject")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RejectLeaveRequest(int leaveRequestId, RejectLeaveRequestDto dto)
    {
        try
        {
            dto.LeaveRequestId = leaveRequestId;
            var result = await _service.RejectLeaveRequestAsync(leaveRequestId, dto);
            return Ok(ApiResponse<LeaveRequestResponseDto>.SuccessResponse(result, "Leave request rejected successfully."));
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

    // ===== Leave History/Reporting Endpoints =====
    [HttpGet("requests/history/employee/{employeeId}")]
    [Authorize(Roles = "Employee,Manager,HR,Admin")]
    public async Task<IActionResult> GetEmployeeLeaveHistory(int employeeId)
    {
        try
        {
            // Verify that the user is either accessing their own history or is an admin
            var employeeIdClaim = User.FindFirst("employeeId")?.Value;
            var userRole = User.FindFirst("http://schemas.microsoft.com/ws/2008/06/identity/claims/role")?.Value ?? 
                          User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;
            
            if (!int.TryParse(employeeIdClaim, out var currentEmployeeId))
            {
                return Unauthorized(ApiResponse<string>.FailResponse("Unable to identify current employee."));
            }

            // Allow access if user is admin or accessing their own history
            if (userRole != "Admin" && currentEmployeeId != employeeId)
            {
                return Forbid();
            }

            var result = await _service.GetEmployeeLeaveHistoryAsync(employeeId);
            return Ok(ApiResponse<List<LeaveRequestResponseDto>>.SuccessResponse(result));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(ApiResponse<string>.FailResponse(ex.Message));
        }
    }

    [HttpGet("requests/all")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAllLeaves()
    {
        var result = await _service.GetAllLeavesAsync();
        return Ok(ApiResponse<List<LeaveRequestResponseDto>>.SuccessResponse(result));
    }
}
