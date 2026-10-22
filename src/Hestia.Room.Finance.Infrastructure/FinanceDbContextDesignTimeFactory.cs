using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Hestia.Room.Finance.Infrastructure;

/// <summary>
/// Used only by the <c>dotnet ef</c> tooling (migrations), which needs a context outside of any application.
/// Creating a migration never opens the connection; applying one does, so the connection string comes from the
/// <c>HESTIA_FINANCE_CONNECTION</c> environment variable, or from <c>dotnet ef database update --connection</c>.
/// No connection string, and no secret, lives in the code.
/// </summary>
internal sealed class FinanceDbContextDesignTimeFactory : IDesignTimeDbContextFactory<FinanceDbContext>
{
    private const string ConnectionVariable = "HESTIA_FINANCE_CONNECTION";

    public FinanceDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable) ?? "Server=not-configured";
        var options = new DbContextOptionsBuilder<FinanceDbContext>().UseRoomSqlServer(connectionString).Options;
        return new FinanceDbContext(options);
    }
}
