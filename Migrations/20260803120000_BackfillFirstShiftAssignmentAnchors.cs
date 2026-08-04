using HRMS_BACKEND.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS_BACKEND.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260803120000_BackfillFirstShiftAssignmentAnchors")]
public partial class BackfillFirstShiftAssignmentAnchors : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Pull FirstShiftAssignmentDate back using earliest attendance with a clock-in.
        migrationBuilder.Sql(@"
            UPDATE e
            SET FirstShiftAssignmentDate = att.FirstDate
            FROM Employees AS e
            INNER JOIN (
                SELECT EmployeeId, MIN([Date]) AS FirstDate
                FROM EmployeeAttendances
                WHERE ClockIn IS NOT NULL
                GROUP BY EmployeeId
            ) AS att ON att.EmployeeId = e.Id
            WHERE e.FirstShiftAssignmentDate IS NULL OR e.FirstShiftAssignmentDate > att.FirstDate;");

        // Align the earliest assignment row when migration stamped EffectiveFrom too late.
        migrationBuilder.Sql(@"
            UPDATE esa
            SET esa.EffectiveFrom = att.FirstDate
            FROM EmployeeShiftAssignments AS esa
            INNER JOIN (
                SELECT EmployeeId, MIN([Date]) AS FirstDate
                FROM EmployeeAttendances
                WHERE ClockIn IS NOT NULL
                GROUP BY EmployeeId
            ) AS att ON att.EmployeeId = esa.EmployeeId
            INNER JOIN (
                SELECT EmployeeId, MIN(EffectiveFrom) AS MinEffectiveFrom
                FROM EmployeeShiftAssignments
                GROUP BY EmployeeId
            ) AS first ON first.EmployeeId = esa.EmployeeId
                AND esa.EffectiveFrom = first.MinEffectiveFrom
            WHERE esa.EffectiveFrom > att.FirstDate;");

        // For employees with assignments but no attendance, seed from joining date when missing.
        migrationBuilder.Sql(@"
            UPDATE e
            SET FirstShiftAssignmentDate = CAST(e.JoiningDate AS date)
            FROM Employees AS e
            WHERE e.FirstShiftAssignmentDate IS NULL
              AND (e.ShiftId IS NOT NULL OR EXISTS (
                    SELECT 1 FROM EmployeeShiftAssignments esa WHERE esa.EmployeeId = e.Id
              ))
              AND CAST(e.JoiningDate AS date) <= CAST(GETDATE() AS date);");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Data-only migration; no schema rollback.
    }
}
