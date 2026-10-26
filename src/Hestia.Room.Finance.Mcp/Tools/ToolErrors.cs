namespace Hestia.Room.Finance.Mcp.Tools;

/// <summary>
/// The messages of the tool errors shared by the input tools of a month and of the plan. The plan view explains some of
/// them in French (REFUSALS in plan.ts): a test keeps the texts equal.
/// </summary>
internal static class ToolErrors
{
    /// <summary>A budget has no dates: it is in the plan or not, whatever the month.</summary>
    public const string UnknownBudget = "No budget with this identifier in the plan.";

    public const string UnknownLine = "No recurring line with this identifier in the plan.";

    public const string BudgetNameTaken = "Another budget already has this name.";

    public const string LineNameTaken = "Another recurring line already has this name over the same period.";

    public const string ActualsChanged =
        "The actuals of this budget changed since the confirmation: nothing was deleted. Show the plan again before deleting.";

    public const string CorrectionsChanged =
        "The corrections of this line changed since the confirmation: nothing was deleted. Show the plan again before deleting.";
}
