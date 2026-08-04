using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS_BACKEND.Migrations
{
    /// <inheritdoc />
    public partial class WorkingDaysEffectiveDatingAndProductionFixes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_WorkingDays",
                table: "WorkingDays");

            migrationBuilder.AddColumn<int>(
                name: "Id",
                table: "WorkingDays",
                type: "int",
                nullable: false,
                defaultValue: 0)
                .Annotation("SqlServer:Identity", "1, 1");

            migrationBuilder.AddColumn<DateOnly>(
                name: "EffectiveToDate",
                table: "WorkingDays",
                type: "date",
                nullable: true);

            migrationBuilder.AddPrimaryKey(
                name: "PK_WorkingDays",
                table: "WorkingDays",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "UX_WorkingDays_Employee_EffectiveFrom",
                table: "WorkingDays",
                columns: new[] { "EmployeeId", "EffectiveFromDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_WorkingDays",
                table: "WorkingDays");

            migrationBuilder.DropIndex(
                name: "UX_WorkingDays_Employee_EffectiveFrom",
                table: "WorkingDays");

            migrationBuilder.DropColumn(
                name: "Id",
                table: "WorkingDays");

            migrationBuilder.DropColumn(
                name: "EffectiveToDate",
                table: "WorkingDays");

            migrationBuilder.AddPrimaryKey(
                name: "PK_WorkingDays",
                table: "WorkingDays",
                columns: new[] { "EmployeeId", "EffectiveFromDate" });
        }
    }
}
