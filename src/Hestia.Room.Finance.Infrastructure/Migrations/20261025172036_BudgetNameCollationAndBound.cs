using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hestia.Room.Finance.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BudgetNameCollationAndBound : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Budgets_PlannedAmount",
                schema: "finance",
                table: "Budgets");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "finance",
                table: "Budgets",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                collation: "SQL_Latin1_General_CP1_CI_AS",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Budgets_PlannedAmount",
                schema: "finance",
                table: "Budgets",
                sql: "[PlannedAmount] > 0 AND [PlannedAmount] <= 999999999.99");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Budgets_PlannedAmount",
                schema: "finance",
                table: "Budgets");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "finance",
                table: "Budgets",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldCollation: "SQL_Latin1_General_CP1_CI_AS");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Budgets_PlannedAmount",
                schema: "finance",
                table: "Budgets",
                sql: "[PlannedAmount] > 0");
        }
    }
}
