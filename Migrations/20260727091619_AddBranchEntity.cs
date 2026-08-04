using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HRMS_BACKEND.Migrations
{
    /// <inheritdoc />
    public partial class AddBranchEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Create Branches table first
            migrationBuilder.CreateTable(
                name: "Branches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Branches", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Branches_Name",
                table: "Branches",
                column: "Name",
                unique: true);

            // 2. Insert default "Main" branch if none exists
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM Branches WHERE Name = 'Main')
                BEGIN
                    INSERT INTO Branches (Name, City, Phone, Address, CreatedAt, IsActive)
                    VALUES ('Main', 'Headquarters', NULL, NULL, GETUTCDATE(), 1);
                END
            ");

            // 3. Add BranchId column to Departments as nullable first, or with default matching 'Main' branch
            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "Departments",
                type: "int",
                nullable: true);

            // Backfill existing Departments to point to 'Main' branch
            migrationBuilder.Sql(@"
                DECLARE @MainBranchId INT;
                SELECT TOP 1 @MainBranchId = Id FROM Branches WHERE Name = 'Main';
                IF @MainBranchId IS NOT NULL
                BEGIN
                    UPDATE Departments SET BranchId = @MainBranchId WHERE BranchId IS NULL;
                END
            ");

            // Alter column to non-nullable
            migrationBuilder.AlterColumn<int>(
                name: "BranchId",
                table: "Departments",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            // 4. Add BranchId column to Employees
            migrationBuilder.AddColumn<int>(
                name: "BranchId",
                table: "Employees",
                type: "int",
                nullable: true);

            // Backfill Employees' BranchId from their Department's BranchId (or 'Main' branch)
            migrationBuilder.Sql(@"
                DECLARE @MainBranchId INT;
                SELECT TOP 1 @MainBranchId = Id FROM Branches WHERE Name = 'Main';
                
                UPDATE e
                SET e.BranchId = d.BranchId
                FROM Employees e
                INNER JOIN Departments d ON e.DepartmentId = d.Id
                WHERE e.BranchId IS NULL;

                UPDATE Employees SET BranchId = @MainBranchId WHERE BranchId IS NULL AND @MainBranchId IS NOT NULL;
            ");

            // 5. Create Indexes & Foreign Keys
            migrationBuilder.CreateIndex(
                name: "IX_Employees_BranchId",
                table: "Employees",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Departments_BranchId",
                table: "Departments",
                column: "BranchId");

            migrationBuilder.AddForeignKey(
                name: "FK_Departments_Branches_BranchId",
                table: "Departments",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Employees_Branches_BranchId",
                table: "Employees",
                column: "BranchId",
                principalTable: "Branches",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Departments_Branches_BranchId",
                table: "Departments");

            migrationBuilder.DropForeignKey(
                name: "FK_Employees_Branches_BranchId",
                table: "Employees");

            migrationBuilder.DropTable(
                name: "Branches");

            migrationBuilder.DropIndex(
                name: "IX_Employees_BranchId",
                table: "Employees");

            migrationBuilder.DropIndex(
                name: "IX_Departments_BranchId",
                table: "Departments");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "Employees");

            migrationBuilder.DropColumn(
                name: "BranchId",
                table: "Departments");
        }
    }
}
