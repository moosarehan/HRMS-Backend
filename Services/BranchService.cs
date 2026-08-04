using HRMS_BACKEND.Data;
using HRMS_BACKEND.Dto.Branch;
using HRMS_BACKEND.Dto.Department;
using HRMS_BACKEND.Entities;
using HRMS_BACKEND.Entities.Enums;
using HRMS_BACKEND.IServices;
using Microsoft.EntityFrameworkCore;

namespace HRMS_BACKEND.Services;

public class BranchService : IBranchService
{
    private readonly AppDbContext _db;
    public BranchService(AppDbContext db) => _db = db;

    public async Task<List<BranchResponseDto>> GetAllAsync(Role currentRole, int currentId)
    {
        if (currentRole == Role.Admin)
        {
            return await _db.Branches
                .Select(b => new BranchResponseDto
                {
                    Id = b.Id,
                    Name = b.Name,
                    City = b.City,
                    Phone = b.Phone,
                    Address = b.Address,
                    IsActive = b.IsActive,
                    CreatedAt = b.CreatedAt,
                    DepartmentCount = b.Departments.Count,
                    EmployeeCount = _db.Employees.Count(e => e.BranchId == b.Id)
                })
                .ToListAsync();
        }

        // HR, Manager, Employee -> only their own branch
        var employee = await _db.Employees.FindAsync(currentId);
        if (employee?.BranchId == null)
            return new List<BranchResponseDto>();

        var branch = await _db.Branches
            .Where(b => b.Id == employee.BranchId)
            .Select(b => new BranchResponseDto
            {
                Id = b.Id,
                Name = b.Name,
                City = b.City,
                Phone = b.Phone,
                Address = b.Address,
                IsActive = b.IsActive,
                CreatedAt = b.CreatedAt,
                DepartmentCount = b.Departments.Count,
                EmployeeCount = _db.Employees.Count(e => e.BranchId == b.Id)
            })
            .FirstOrDefaultAsync();

        return branch is null ? new List<BranchResponseDto>() : new List<BranchResponseDto> { branch };
    }

    public async Task<BranchResponseDto> GetByIdAsync(int id)
    {
        var b = await _db.Branches
            .Where(b => b.Id == id)
            .Select(b => new BranchResponseDto
            {
                Id = b.Id,
                Name = b.Name,
                City = b.City,
                Phone = b.Phone,
                Address = b.Address,
                IsActive = b.IsActive,
                CreatedAt = b.CreatedAt,
                DepartmentCount = b.Departments.Count,
                EmployeeCount = _db.Employees.Count(e => e.BranchId == b.Id)
            })
            .FirstOrDefaultAsync()
            ?? throw new KeyNotFoundException("Branch not found.");

        return b;
    }

    public async Task<List<DepartmentResponseDto>> GetDepartmentsByBranchAsync(int branchId)
    {
        if (!await _db.Branches.AnyAsync(b => b.Id == branchId))
            throw new KeyNotFoundException("Branch not found.");

        return await _db.Departments
            .Where(d => d.BranchId == branchId)
            .Select(d => new DepartmentResponseDto
            {
                Id = d.Id,
                Name = d.Name,
                Description = d.Description,
                EmployeeCount = d.Employees.Count,
                BranchId = d.BranchId,
                BranchName = d.Branch.Name
            })
            .ToListAsync();
    }

    public async Task<BranchResponseDto> CreateAsync(CreateBranchDto dto)
    {
        if (await _db.Branches.AnyAsync(b => b.Name == dto.Name))
            throw new InvalidOperationException($"A branch named '{dto.Name}' already exists.");

        var branch = new Branch
        {
            Name = dto.Name,
            City = dto.City,
            Phone = dto.Phone,
            Address = dto.Address,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        _db.Branches.Add(branch);
        await _db.SaveChangesAsync();

        return new BranchResponseDto
        {
            Id = branch.Id,
            Name = branch.Name,
            City = branch.City,
            Phone = branch.Phone,
            Address = branch.Address,
            IsActive = branch.IsActive,
            CreatedAt = branch.CreatedAt,
            DepartmentCount = 0,
            EmployeeCount = 0
        };
    }

    public async Task<BranchResponseDto> UpdateAsync(int id, UpdateBranchDto dto)
    {
        var branch = await _db.Branches.FindAsync(id)
            ?? throw new KeyNotFoundException("Branch not found.");

        // Enforce unique name (excluding self)
        if (await _db.Branches.AnyAsync(b => b.Name == dto.Name && b.Id != id))
            throw new InvalidOperationException($"Another branch named '{dto.Name}' already exists.");

        branch.Name = dto.Name;
        branch.City = dto.City;
        branch.Phone = dto.Phone;
        branch.Address = dto.Address;

        await _db.SaveChangesAsync();

        var deptCount = await _db.Departments.CountAsync(d => d.BranchId == id);
        var empCount = await _db.Employees.CountAsync(e => e.BranchId == id);

        return new BranchResponseDto
        {
            Id = branch.Id,
            Name = branch.Name,
            City = branch.City,
            Phone = branch.Phone,
            Address = branch.Address,
            IsActive = branch.IsActive,
            CreatedAt = branch.CreatedAt,
            DepartmentCount = deptCount,
            EmployeeCount = empCount
        };
    }

    public async Task<BranchDeleteImpactDto> GetDeleteImpactAsync(int id)
    {
        var branch = await _db.Branches
            .Include(b => b.Departments)
                .ThenInclude(d => d.Employees)
            .FirstOrDefaultAsync(b => b.Id == id)
            ?? throw new KeyNotFoundException("Branch not found.");

        return new BranchDeleteImpactDto
        {
            BranchName = branch.Name,
            DepartmentCount = branch.Departments.Count,
            EmployeeCount = branch.Departments.Sum(d => d.Employees.Count)
        };
    }

    public async Task DeleteAsync(int id)
    {
        var branch = await _db.Branches.Include(b => b.Departments).FirstOrDefaultAsync(b => b.Id == id)
            ?? throw new KeyNotFoundException("Branch not found.");

        // STEP 1: Get ALL employees assigned to this branch (whether or not they have a department)
        var allEmployeesInBranch = await _db.Employees
            .Where(e => e.BranchId == id)
            .ToListAsync();

        // STEP 2: Clear BranchId and DepartmentId from ALL employees in this branch
        foreach (var emp in allEmployeesInBranch)
        {
            emp.BranchId = null;
            emp.DepartmentId = null;
        }

        // STEP 3: Get all departments in this branch
        var departmentsInBranch = await _db.Departments
            .Where(d => d.BranchId == id)
            .ToListAsync();

        // STEP 4: Delete all departments in this branch
        _db.Departments.RemoveRange(departmentsInBranch);

        // STEP 5: Delete the branch
        _db.Branches.Remove(branch);
        
        // STEP 6: Save all changes
        await _db.SaveChangesAsync();
    }
}
