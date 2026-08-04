using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS_BACKEND.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaveManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Create LeaveType table
            migrationBuilder.CreateTable(
                name: "LeaveTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveTypes", x => x.Id);
                });

            // 2. Create LeavePeriod table
            migrationBuilder.CreateTable(
                name: "LeavePeriods",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalAllowedDays = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeavePeriods", x => x.Id);
                });

            // 3. Create LeaveQuota table
            migrationBuilder.CreateTable(
                name: "LeaveQuotas",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    LeaveTypeId = table.Column<int>(type: "int", nullable: false),
                    LeavePeriodId = table.Column<int>(type: "int", nullable: false),
                    AllocatedDays = table.Column<int>(type: "int", nullable: false),
                    UsedDays = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveQuotas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeaveQuotas_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LeaveQuotas_LeaveTypes_LeaveTypeId",
                        column: x => x.LeaveTypeId,
                        principalTable: "LeaveTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeaveQuotas_LeavePeriods_LeavePeriodId",
                        column: x => x.LeavePeriodId,
                        principalTable: "LeavePeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // 4. Create LeaveMaster table
            migrationBuilder.CreateTable(
                name: "LeaveMasters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EmployeeId = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LeaveTypeId = table.Column<int>(type: "int", nullable: false),
                    LeavePeriodId = table.Column<int>(type: "int", nullable: false),
                    NoOfDays = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveMasters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeaveMasters_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LeaveMasters_LeaveTypes_LeaveTypeId",
                        column: x => x.LeaveTypeId,
                        principalTable: "LeaveTypes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LeaveMasters_LeavePeriods_LeavePeriodId",
                        column: x => x.LeavePeriodId,
                        principalTable: "LeavePeriods",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // 5. Create indexes
            migrationBuilder.CreateIndex(
                name: "IX_LeaveQuotas_EmployeeId",
                table: "LeaveQuotas",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveQuotas_LeaveTypeId",
                table: "LeaveQuotas",
                column: "LeaveTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveQuotas_LeavePeriodId",
                table: "LeaveQuotas",
                column: "LeavePeriodId");

            migrationBuilder.CreateIndex(
                name: "UX_LeaveQuota_EmployeeTypePerious",
                table: "LeaveQuotas",
                columns: new[] { "EmployeeId", "LeaveTypeId", "LeavePeriodId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaveMasters_EmployeeId",
                table: "LeaveMasters",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveMasters_LeaveTypeId",
                table: "LeaveMasters",
                column: "LeaveTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveMasters_LeavePeriodId",
                table: "LeaveMasters",
                column: "LeavePeriodId");

            // 6. Seed LeaveTypes (Sick Leave, Annual Leave, Casual Leave)
            migrationBuilder.InsertData(
                table: "LeaveTypes",
                columns: new[] { "Name", "Description" },
                values: new object[,]
                {
                    { "Sick Leave", "Leave taken due to illness or medical reasons" },
                    { "Annual Leave", "Paid annual vacation leave" },
                    { "Casual Leave", "Casual leave for personal reasons" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeaveMasters");

            migrationBuilder.DropTable(
                name: "LeaveQuotas");

            migrationBuilder.DropTable(
                name: "LeavePeriods");

            migrationBuilder.DropTable(
                name: "LeaveTypes");
        }
    }
}
