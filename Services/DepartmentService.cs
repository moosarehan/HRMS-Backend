using HRMS_BACKEND.Data;
using HRMS_BACKEND.Dto.Department;
using HRMS_BACKEND.Entities;
using HRMS_BACKEND.IServices;
using Microsoft.EntityFrameworkCore;

namespace HRMS_BACKEND.Services;

public class DepartmentService : IDepartmentService
{
    private readonly AppDbContext _db;
    public DepartmentService(AppDbContext db) => _db = db;

    public async Task<List<DepartmentResponseDto>> GetAllAsync()
    {
        return await _db.Departments
            .Include(d => d.Branch)
            .Select(d => new DepartmentResponseDto
            {
                Id = d.Id,
                Name = d.Name,
                Description = d.Description,
                EmployeeCount = d.Employees.Count,
                BranchId = d.BranchId,
                BranchName = d.Branch != null ? d.Branch.Name : string.Empty
            }).ToListAsync();
    }

    public async Task<DepartmentResponseDto> CreateAsync(CreateDepartmentDto dto)
    {
        var branch = await _db.Branches.FirstOrDefaultAsync(b => b.Name == dto.BranchName)
            ?? throw new InvalidOperationException($"No branch named '{dto.BranchName}' exists. Please select a valid branch.");

        var dept = new Department
        {
            Name = dto.Name,
            Description = dto.Description,
            BranchId = branch.Id
        };
        _db.Departments.Add(dept);
        await _db.SaveChangesAsync();

        return new DepartmentResponseDto
        {
            Id = dept.Id,
            Name = dept.Name,
            Description = dept.Description,
            EmployeeCount = 0,
            BranchId = branch.Id,
            BranchName = branch.Name
        };
    }

    public async Task<DepartmentResponseDto> UpdateAsync(int id, CreateDepartmentDto dto)
    {
        var dept = await _db.Departments.Include(d => d.Branch).FirstOrDefaultAsync(d => d.Id == id)
            ?? throw new KeyNotFoundException("Department not found.");

        dept.Name = dto.Name;
        dept.Description = dto.Description;
        await _db.SaveChangesAsync();

        var empCount = await _db.Employees.CountAsync(e => e.DepartmentId == id);

        return new DepartmentResponseDto
        {
            Id = dept.Id,
            Name = dept.Name,
            Description = dept.Description,
            EmployeeCount = empCount,
            BranchId = dept.BranchId,
            BranchName = dept.Branch != null ? dept.Branch.Name : string.Empty
        };
    }

    public async Task DeleteAsync(int id)
    {
        var dept = await _db.Departments.Include(d => d.Employees).FirstOrDefaultAsync(d => d.Id == id)
            ?? throw new KeyNotFoundException("Department not found.");
        
        // When deleting a department, only clear DepartmentId, NOT BranchId
        // BranchId should remain as it is independent from DepartmentId
        if (dept.Employees.Count > 0)
        {
            foreach (var emp in dept.Employees)
            {
                emp.DepartmentId = null;
                // DO NOT clear BranchId - it's independent
            }
        }
        
        _db.Departments.Remove(dept);
        await _db.SaveChangesAsync();
    }
}
