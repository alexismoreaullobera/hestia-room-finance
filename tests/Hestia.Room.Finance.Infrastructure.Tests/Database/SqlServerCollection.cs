using Hestia.Room.Finance.Testing;

namespace Hestia.Room.Finance.Infrastructure.Tests.Database;

/// <summary>Every class of this collection shares the same SQL Server container (see <see cref="SqlServerFixture"/>).</summary>
[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerFixture>
{
    public const string Name = "SQL Server";
}
