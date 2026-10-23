using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Hestia.Room.Finance.Infrastructure;

/// <summary>
/// The SQL Server settings of the room, shared by every place that creates the context (application, design-time
/// factory, tests): the migrations history table also lives in the room schema, so that several rooms sharing a
/// database never mix their migration histories.
/// </summary>
public static class FinanceDbContextOptions
{
    /// <summary>For <c>AddDbContext</c>, whose callback gives a builder without the context type.</summary>
    public static DbContextOptionsBuilder UseRoomSqlServer(this DbContextOptionsBuilder options, string connectionString) =>
        options.UseSqlServer(connectionString, RoomSettings);

    /// <summary>For the places that build the options themselves and need <c>DbContextOptions&lt;FinanceDbContext&gt;</c>.</summary>
    public static DbContextOptionsBuilder<FinanceDbContext> UseRoomSqlServer(
        this DbContextOptionsBuilder<FinanceDbContext> options, string connectionString) =>
        options.UseSqlServer(connectionString, RoomSettings);

    private static void RoomSettings(SqlServerDbContextOptionsBuilder sql) =>
        sql.MigrationsHistoryTable("__EFMigrationsHistory", FinanceDbContext.Schema);
}
