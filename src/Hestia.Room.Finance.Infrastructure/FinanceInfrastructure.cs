using Hestia.Room.Finance.Domain;
using Hestia.Room.Finance.Infrastructure.Demo;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hestia.Room.Finance.Infrastructure;

/// <summary>Plugs the persistence of the room into the dependency injection container of the host.</summary>
public static class FinanceInfrastructure
{
    public static IServiceCollection AddFinanceInfrastructure(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        // Scoped (the default of AddDbContext): one context per dependency injection scope, which a web host creates
        // per request, never shared between two scopes since a DbContext is not thread-safe. The reader and the seeder
        // use it, so they are scoped too.
        services.AddDbContext<FinanceDbContext>(options => options.UseRoomSqlServer(connectionString));
        services.AddScoped<FinancePlanReader>();
        services.AddScoped<DemoDataSeeder>();
        return services;
    }

    /// <summary>
    /// For development only (the host decides): applies the pending migrations, then seeds the demo plan if the
    /// database is empty. Returns true if the demo was seeded. In production, migrations are applied by the
    /// deployment pipeline, never by the application at startup.
    /// </summary>
    public static async Task<bool> PrepareDevelopmentDatabaseAsync(
        this IServiceProvider services, YearMonth currentMonth, CancellationToken cancellationToken = default)
    {
        // Called outside any scope (at startup): a dedicated scope for the scoped services, disposed right after.
        await using var scope = services.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<FinanceDbContext>().Database.MigrateAsync(cancellationToken);
        return await scope.ServiceProvider.GetRequiredService<DemoDataSeeder>().SeedIfEmptyAsync(currentMonth, cancellationToken);
    }
}
