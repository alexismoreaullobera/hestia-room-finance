using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hestia.Room.Finance.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AmountBounds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_RecurringOverrides_ActualAmount",
                schema: "finance",
                table: "RecurringOverrides");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BudgetActuals_Amount",
                schema: "finance",
                table: "BudgetActuals");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RecurringOverrides_ActualAmount",
                schema: "finance",
                table: "RecurringOverrides",
                sql: "[ActualAmount] IS NULL OR ([ActualAmount] > 0 AND [ActualAmount] <= 999999999.99)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MonthPlans_StartingBalance",
                schema: "finance",
                table: "MonthPlans",
                sql: "[StartingBalance] BETWEEN -999999999.99 AND 999999999.99");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BudgetActuals_Amount",
                schema: "finance",
                table: "BudgetActuals",
                sql: "[Amount] BETWEEN 0 AND 999999999.99");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_RecurringOverrides_ActualAmount",
                schema: "finance",
                table: "RecurringOverrides");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MonthPlans_StartingBalance",
                schema: "finance",
                table: "MonthPlans");

            migrationBuilder.DropCheckConstraint(
                name: "CK_BudgetActuals_Amount",
                schema: "finance",
                table: "BudgetActuals");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RecurringOverrides_ActualAmount",
                schema: "finance",
                table: "RecurringOverrides",
                sql: "[ActualAmount] IS NULL OR [ActualAmount] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_BudgetActuals_Amount",
                schema: "finance",
                table: "BudgetActuals",
                sql: "[Amount] >= 0");
        }
    }
}
