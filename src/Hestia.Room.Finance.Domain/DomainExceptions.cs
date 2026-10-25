namespace Hestia.Room.Finance.Domain;

// The rules that involve more than one value, as exceptions of their own: a caller (an MCP tool) tells them apart by
// type, never by the name of a parameter. They stay ArgumentExceptions, with the parameter at fault.

/// <summary>No budget with this identifier in the plan.</summary>
public sealed class UnknownBudgetException(string paramName)
    : ArgumentException("No budget with this identifier.", paramName);

/// <summary>No recurring line with this identifier due in the month.</summary>
public sealed class LineNotDueException(string paramName)
    : ArgumentException("No recurring line with this identifier due in the month.", paramName);

/// <summary>Another item of the plan already has this name (ignoring case).</summary>
public sealed class NameTakenException(string paramName)
    : ArgumentException("This name is already used.", paramName);
