using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hestia.Room.Finance.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "finance");

            migrationBuilder.CreateTable(
                name: "Budgets",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PlannedAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Budgets", x => x.Id);
                    table.CheckConstraint("CK_Budgets_PlannedAmount", "[PlannedAmount] > 0");
                });

            migrationBuilder.CreateTable(
                name: "MonthPlans",
                schema: "finance",
                columns: table => new
                {
                    Month = table.Column<DateOnly>(type: "date", nullable: false),
                    StartingBalance = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonthPlans", x => x.Month);
                    table.CheckConstraint("CK_MonthPlans_Month", "DAY([Month]) = 1");
                });

            migrationBuilder.CreateTable(
                name: "RecurringLines",
                schema: "finance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Frequency = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DayOfMonth = table.Column<int>(type: "int", nullable: false),
                    AnchorMonth = table.Column<int>(type: "int", nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Category = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IsSubscription = table.Column<bool>(type: "bit", nullable: false),
                    CommitmentEndDate = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringLines", x => x.Id);
                    table.CheckConstraint("CK_RecurringLines_Amount", "[Amount] > 0");
                    table.CheckConstraint("CK_RecurringLines_AnchorMonth", "[AnchorMonth] IS NULL OR [AnchorMonth] BETWEEN 1 AND 12");
                    table.CheckConstraint("CK_RecurringLines_DayOfMonth", "[DayOfMonth] BETWEEN 1 AND 31");
                });

            migrationBuilder.CreateTable(
                name: "BudgetActuals",
                schema: "finance",
                columns: table => new
                {
                    BudgetId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Month = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BudgetActuals", x => new { x.BudgetId, x.Month });
                    table.CheckConstraint("CK_BudgetActuals_Amount", "[Amount] >= 0");
                    table.CheckConstraint("CK_BudgetActuals_Month", "DAY([Month]) = 1");
                    table.ForeignKey(
                        name: "FK_BudgetActuals_Budgets_BudgetId",
                        column: x => x.BudgetId,
                        principalSchema: "finance",
                        principalTable: "Budgets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RecurringOverrides",
                schema: "finance",
                columns: table => new
                {
                    LineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Month = table.Column<DateOnly>(type: "date", nullable: false),
                    ActualAmount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecurringOverrides", x => new { x.LineId, x.Month });
                    table.CheckConstraint("CK_RecurringOverrides_ActualAmount", "[ActualAmount] IS NULL OR [ActualAmount] > 0");
                    table.CheckConstraint("CK_RecurringOverrides_Month", "DAY([Month]) = 1");
                    table.ForeignKey(
                        name: "FK_RecurringOverrides_RecurringLines_LineId",
                        column: x => x.LineId,
                        principalSchema: "finance",
                        principalTable: "RecurringLines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BudgetActuals",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "MonthPlans",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "RecurringOverrides",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "Budgets",
                schema: "finance");

            migrationBuilder.DropTable(
                name: "RecurringLines",
                schema: "finance");
        }
    }
}
