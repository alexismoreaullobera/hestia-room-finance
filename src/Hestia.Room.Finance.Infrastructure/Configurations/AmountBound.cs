using System.Globalization;
using Hestia.Room.Finance.Domain;

namespace Hestia.Room.Finance.Infrastructure.Configurations;

/// <summary>
/// The amount bound of the domain (<see cref="AmountLimit.Max"/>) written as a SQL literal, for the check constraints of
/// the amounts the user types: a row written outside the domain (by hand, by a script) cannot exceed it either.
/// </summary>
internal static class AmountBound
{
    /// <summary>999999999.99, whatever the culture of the machine that generates the migration.</summary>
    public static readonly string Sql = AmountLimit.Max.ToString(CultureInfo.InvariantCulture);
}
