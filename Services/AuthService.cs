using HRMS_BACKEND.Data;
using HRMS_BACKEND.Dto.Auth;
using HRMS_BACKEND.Entities;
using HRMS_BACKEND.Entities.Enums;
using HRMS_BACKEND.Helpers;
using HRMS_BACKEND.IServices;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace HRMS_BACKEND.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly JwtTokenGenerator _jwtGenerator;
    private readonly PasswordHasher<Employee> _passwordHasher = new();

    public AuthService(AppDbContext db, JwtTokenGenerator jwtGenerator)
    {
        _db = db;
        _jwtGenerator = jwtGenerator;
    }

    public async Task<bool> AnyAdminExistsAsync()
        => await _db.Employees.AnyAsync(e => e.Role == Role.Admin);

    public async Task<AuthResponseDto> RegisterFirstAdminAsync(RegisterAdminDto dto)
    {
        if (await _db.Employees.AnyAsync(e => e.Email == dto.Email))
            throw new InvalidOperationException("Email already in use.");

        var admin = new Employee
        {
            FullName = dto.FullName,
            Email = dto.Email,
            Role = Role.Admin,
            Phone = dto.Phone,
            Address = dto.Address,
            Salary = dto.Salary,
            JoiningDate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        admin.PasswordHash = _passwordHasher.HashPassword(admin, dto.Password);

        _db.Employees.Add(admin);
        await _db.SaveChangesAsync();

        return await BuildAuthResponse(admin);
    }

    public async Task ResetPasswordAsync(ResetPasswordDto dto)
    {
        if (dto.NewPassword != dto.ConfirmPassword)
            throw new InvalidOperationException("Passwords do not match.");

        if (dto.NewPassword.Length < 6)
            throw new InvalidOperationException("Password must be at least 6 characters.");

        var normalizedEmail = dto.Email.Trim().ToLower();
        var employee = await _db.Employees
            .FirstOrDefaultAsync(e => e.Email.ToLower() == normalizedEmail && e.IsActive);

        if (employee is null)
            throw new InvalidOperationException("No active account found with that email address.");

        employee.PasswordHash = _passwordHasher.HashPassword(employee, dto.NewPassword);
        await _db.SaveChangesAsync();
    }

    public async Task<AuthResponseDto> LoginAsync(LoginDto dto)
    {
        var normalizedEmail = (dto.Email ?? "").Trim().ToLower();
        var employee = await _db.Employees
            .FirstOrDefaultAsync(e => e.Email.ToLower() == normalizedEmail && e.IsActive);

        if (employee is null)
            throw new UnauthorizedAccessException("Invalid credentials.");

        var result = _passwordHasher.VerifyHashedPassword(employee, employee.PasswordHash, dto.Password);

        if (result == PasswordVerificationResult.Failed)
            throw new UnauthorizedAccessException("Invalid credentials.");

        return await BuildAuthResponse(employee);
    }

    private async Task<AuthResponseDto> BuildAuthResponse(Employee employee)
    {
        var accessToken = _jwtGenerator.GenerateAccessToken(employee);
        var refreshTokenValue = _jwtGenerator.GenerateRefreshToken();

        _db.RefreshTokens.Add(new RefreshToken
        {
            Token = refreshTokenValue,
            EmployeeId = employee.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(7)
        });
        await _db.SaveChangesAsync();

        return new AuthResponseDto
        {
            Token = accessToken,
            RefreshToken = refreshTokenValue,
            Role = employee.Role.ToString(),
            EmployeeId = employee.Id,
            FullName = employee.FullName
        };
    }
}
