namespace Hestia.Room.Finance.Infrastructure.Configurations;

/// <summary>The columns of the names the user types (budgets, recurring lines): one collation for every one of them.</summary>
internal static class NameColumn
{
    /// <summary>
    /// Case insensitive and accent sensitive, like the domain ("Épargne" and "Epargne" are two names), whatever the
    /// default collation of the database: on Azure SQL it is chosen when the database is created.
    /// </summary>
    public const string Collation = "SQL_Latin1_General_CP1_CI_AS";
}
