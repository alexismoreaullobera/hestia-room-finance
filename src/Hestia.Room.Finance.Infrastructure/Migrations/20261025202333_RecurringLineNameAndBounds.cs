using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hestia.Room.Finance.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RecurringLineNameAndBounds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_RecurringLines_Amount",
                schema: "finance",
                table: "RecurringLines");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "finance",
                table: "RecurringLines",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                collation: "SQL_Latin1_General_CP1_CI_AS",
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.CreateIndex(
                name: "IX_RecurringLines_Name",
                schema: "finance",
                table: "RecurringLines",
                column: "Name",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_RecurringLines_Amount",
                schema: "finance",
                table: "RecurringLines",
                sql: "[Amount] > 0 AND [Amount] <= 999999999.99");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RecurringLines_EndDate",
                schema: "finance",
                table: "RecurringLines",
                sql: "[EndDate] IS NULL OR [EndDate] >= [StartDate]");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RecurringLines_Name",
                schema: "finance",
                table: "RecurringLines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RecurringLines_Amount",
                schema: "finance",
                table: "RecurringLines");

            migrationBuilder.DropCheckConstraint(
                name: "CK_RecurringLines_EndDate",
                schema: "finance",
                table: "RecurringLines");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                schema: "finance",
                table: "RecurringLines",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldCollation: "SQL_Latin1_General_CP1_CI_AS");

            migrationBuilder.AddCheckConstraint(
                name: "CK_RecurringLines_Amount",
                schema: "finance",
                table: "RecurringLines",
                sql: "[Amount] > 0");
        }
    }
}
