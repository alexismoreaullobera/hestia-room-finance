using System.ComponentModel;
using System.Globalization;
using Hestia.Room.Finance.Domain;
using Hestia.Room.Finance.Infrastructure;
using ModelContextProtocol;
using ModelContextProtocol.Extensions.Apps;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace Hestia.Room.Finance.Mcp.Tools;

/// <summary>
/// The input tools of the plan view: the budgets and the recurring lines, created, changed, ended or deleted. Same
/// contract as <see cref="FinanceInputTools"/> (visibility "app", not access control, every value through the domain),
/// with the rules of the whole plan: each change is checked against every budget or every line, ended ones included,
/// then the plan of the month shown is returned, as get_plan gives it.
/// </summary>
[McpServerToolType]
public sealed class FinancePlanInputTools
{
    /// <summary>A budget has no dates: it is in the plan or not, whatever the month (also for set_budget_actual).</summary>
    internal const string UnknownBudget = "No budget with this identifier in the plan.";

    private const string MonthShown = "Month the view shows, as YYYY-MM: the plan returned is the one of that month.";

    [McpServerTool(Name = "create_budget", Title = "Saisie : nouveau budget", ReadOnly = false,
        Destructive = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true,
        OutputSchemaType = typeof(Outputs.PlanOutput))]
    [Description("Reserved for the plan view, where the user types it: not to be called from a conversation. Adds a budget "
        + "to the plan (a variable expense planned every month), under a name no other budget uses. Then returns the plan, "
        + "as get_plan does for the month given.")]
    // No resource URI: called from a view already shown, it must not make the host open another one.
    [McpAppUi(Visibility = [McpUiToolVisibility.App])]
    public static Task<CallToolResult> CreateBudget(
        FinancePlanWriter writer,
        FinancePlanReader reader,
        [Description(MonthShown)] string month,
        [Description(BudgetNameDescription)] string name,
        [Description(PlannedAmountDescription)] decimal plannedAmount,
        CancellationToken cancellationToken = default) =>
        DecideSaveAndReadAsync(reader, month, Budgets, BudgetRule, plan =>
        {
            var budget = plan.Budgets.Create(Guid.CreateVersion7(), name, plannedAmount);
            return () => writer.AddBudgetAsync(budget, cancellationToken);
        }, cancellationToken);

    [McpServerTool(Name = "update_budget", Title = "Saisie : modification d'un budget", ReadOnly = false,
        Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true,
        OutputSchemaType = typeof(Outputs.PlanOutput))]
    [Description("Reserved for the plan view, where the user types it: not to be called from a conversation. Renames a "
        + "budget or gives it another planned amount, for every month (past ones included). Then returns the plan, as "
        + "get_plan does for the month given.")]
    [McpAppUi(Visibility = [McpUiToolVisibility.App])]
    public static Task<CallToolResult> UpdateBudget(
        FinancePlanWriter writer,
        FinancePlanReader reader,
        [Description(MonthShown)] string month,
        [Description("Identifier of a budget of the plan.")] Guid budgetId,
        [Description(BudgetNameDescription)] string name,
        [Description(PlannedAmountDescription)] decimal plannedAmount,
        CancellationToken cancellationToken = default) =>
        DecideSaveAndReadAsync(reader, month, Budgets, BudgetRule, plan =>
        {
            var budget = plan.Budgets.Update(budgetId, name, plannedAmount);
            return () => writer.UpdateBudgetAsync(budget, cancellationToken);
        }, cancellationToken);

    [McpServerTool(Name = "delete_budget", Title = "Saisie : suppression d'un budget", ReadOnly = false,
        Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true,
        OutputSchemaType = typeof(Outputs.PlanOutput))]
    [Description("Reserved for the plan view, where the user confirms it: not to be called from a conversation. Deletes a "
        + "budget entered by mistake, and with it every actual entered for it (a budget has no dates). Then returns the "
        + "plan, as get_plan does for the month given.")]
    [McpAppUi(Visibility = [McpUiToolVisibility.App])]
    public static Task<CallToolResult> DeleteBudget(
        FinancePlanWriter writer,
        FinancePlanReader reader,
        [Description(MonthShown)] string month,
        [Description("Identifier of a budget of the plan.")] Guid budgetId,
        [Description("The number of actuals of this budget the user was told would go with it (its actualsEntered in "
            + "get_plan): the deletion is refused when it changed since.")] int actualsEntered,
        CancellationToken cancellationToken = default) =>
        DecideSaveAndReadAsync(reader, month, Budgets, BudgetRule, plan =>
        {
            plan.Budgets.EnsureExists(budgetId);
            return async () =>
            {
                // Never more deleted than the confirmation said: an actual entered meanwhile (another view, another host)
                // would go unseen with the budget.
                var counts = await reader.CountActualsByBudgetAsync(cancellationToken);
                if (counts.GetValueOrDefault(budgetId) != actualsEntered)
                    throw new McpException("The actuals of this budget changed since the confirmation: nothing was deleted. "
                        + "Show the plan again before deleting.");
                await writer.DeleteBudgetAsync(budgetId, cancellationToken);
            };
        }, cancellationToken);

    [McpServerTool(Name = "create_line", Title = "Saisie : nouvelle ligne récurrente", ReadOnly = false,
        Destructive = false, Idempotent = false, OpenWorld = false, UseStructuredContent = true,
        OutputSchemaType = typeof(Outputs.PlanOutput))]
    [Description("Reserved for the plan view, where the user types it: not to be called from a conversation. Adds a "
        + "recurring line to the plan (an income, a fixed expense or a subscription), under a name no other line uses. "
        + "Then returns the plan, as get_plan does for the month given.")]
    [McpAppUi(Visibility = [McpUiToolVisibility.App])]
    public static Task<CallToolResult> CreateLine(
        FinancePlanWriter writer,
        FinancePlanReader reader,
        [Description(MonthShown)] string month,
        [Description(KindDescription)] FlowKind kind,
        [Description(LineNameDescription)] string name,
        [Description(AmountDescription)] decimal amount,
        [Description(FrequencyDescription)] Frequency frequency,
        [Description(DayOfMonthDescription)] int dayOfMonth,
        [Description(AnchorMonthDescription)] int? anchorMonth,
        [Description(StartDateDescription)] DateOnly startDate,
        [Description(EndDateDescription)] DateOnly? endDate,
        [Description(CategoryDescription)] Category category,
        [Description(IsSubscriptionDescription)] bool isSubscription,
        [Description(CommitmentEndDateDescription)] DateOnly? commitmentEndDate,
        CancellationToken cancellationToken = default) =>
        DecideSaveAndReadAsync(reader, month, Lines, LineRule, plan =>
        {
            var line = plan.Lines.Create(NewLine(Guid.CreateVersion7(), kind, name, amount, frequency, dayOfMonth, anchorMonth,
                startDate, endDate, category, isSubscription, commitmentEndDate));
            return () => writer.AddLineAsync(line, cancellationToken);
        }, cancellationToken);

    [McpServerTool(Name = "update_line", Title = "Saisie : modification d'une ligne récurrente", ReadOnly = false,
        Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true,
        OutputSchemaType = typeof(Outputs.PlanOutput))]
    [Description("Reserved for the plan view, where the user types it: not to be called from a conversation. Changes a "
        + "recurring line for every month (past ones included: to change it from a month on, end it and create another). "
        + "Its corrections stay: one of a month where it no longer falls due is ignored. Then returns the plan, as "
        + "get_plan does for the month given.")]
    [McpAppUi(Visibility = [McpUiToolVisibility.App])]
    public static Task<CallToolResult> UpdateLine(
        FinancePlanWriter writer,
        FinancePlanReader reader,
        [Description(MonthShown)] string month,
        [Description("Identifier of a recurring line of the plan.")] Guid lineId,
        [Description(KindDescription)] FlowKind kind,
        [Description(LineNameDescription)] string name,
        [Description(AmountDescription)] decimal amount,
        [Description(FrequencyDescription)] Frequency frequency,
        [Description(DayOfMonthDescription)] int dayOfMonth,
        [Description(AnchorMonthDescription)] int? anchorMonth,
        [Description(StartDateDescription)] DateOnly startDate,
        [Description(EndDateDescription)] DateOnly? endDate,
        [Description(CategoryDescription)] Category category,
        [Description(IsSubscriptionDescription)] bool isSubscription,
        [Description(CommitmentEndDateDescription)] DateOnly? commitmentEndDate,
        CancellationToken cancellationToken = default) =>
        DecideSaveAndReadAsync(reader, month, Lines, LineRule, plan =>
        {
            var line = plan.Lines.Update(NewLine(lineId, kind, name, amount, frequency, dayOfMonth, anchorMonth,
                startDate, endDate, category, isSubscription, commitmentEndDate));
            return () => writer.UpdateLineAsync(line, cancellationToken);
        }, cancellationToken);

    [McpServerTool(Name = "end_line", Title = "Saisie : fin d'une ligne récurrente", ReadOnly = false,
        Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true,
        OutputSchemaType = typeof(Outputs.PlanOutput))]
    [Description("Reserved for the plan view, where the user chooses the date: not to be called from a conversation. Ends a "
        + "recurring line that stops (a subscription cancelled, a rent after a move): it runs until that date, then leaves "
        + "the months after; its past stays. Then returns the plan, as get_plan does for the month given.")]
    [McpAppUi(Visibility = [McpUiToolVisibility.App])]
    public static Task<CallToolResult> EndLine(
        FinancePlanWriter writer,
        FinancePlanReader reader,
        [Description(MonthShown)] string month,
        [Description("Identifier of a recurring line of the plan.")] Guid lineId,
        [Description("Last day the line runs, as YYYY-MM-DD, never before its start date.")] DateOnly endDate,
        CancellationToken cancellationToken = default) =>
        DecideSaveAndReadAsync(reader, month, Lines, EndRule, plan =>
        {
            var line = plan.Lines.End(lineId, endDate);
            return () => writer.UpdateLineAsync(line, cancellationToken);
        }, cancellationToken);

    [McpServerTool(Name = "delete_line", Title = "Saisie : suppression d'une ligne récurrente", ReadOnly = false,
        Destructive = true, Idempotent = true, OpenWorld = false, UseStructuredContent = true,
        OutputSchemaType = typeof(Outputs.PlanOutput))]
    [Description("Reserved for the plan view, where the user confirms it: not to be called from a conversation. Deletes a "
        + "recurring line entered by mistake, and with it every correction entered for it, in every month (a line that "
        + "stops is ended instead). Then returns the plan, as get_plan does for the month given.")]
    [McpAppUi(Visibility = [McpUiToolVisibility.App])]
    public static Task<CallToolResult> DeleteLine(
        FinancePlanWriter writer,
        FinancePlanReader reader,
        [Description(MonthShown)] string month,
        [Description("Identifier of a recurring line of the plan.")] Guid lineId,
        [Description("The number of corrections of this line the user was told would go with it (its overridesEntered in "
            + "get_plan): the deletion is refused when it changed since.")] int overridesEntered,
        CancellationToken cancellationToken = default) =>
        DecideSaveAndReadAsync(reader, month, Lines, LineRule, plan =>
        {
            plan.Lines.EnsureExists(lineId);
            return async () =>
            {
                // Never more deleted than the confirmation said, as for a budget.
                var counts = await reader.CountOverridesByLineAsync(cancellationToken);
                if (counts.GetValueOrDefault(lineId) != overridesEntered)
                    throw new McpException("The corrections of this line changed since the confirmation: nothing was deleted. "
                        + "Show the plan again before deleting.");
                await writer.DeleteLineAsync(lineId, cancellationToken);
            };
        }, cancellationToken);

    /// <summary>
    /// The line as typed: its own rules are checked by its constructor. A commitment end without a subscription is refused
    /// rather than dropped: the view never sends one, a client that does has a mistake to see.
    /// </summary>
    private static RecurringLine NewLine(Guid id, FlowKind kind, string name, decimal amount, Frequency frequency, int dayOfMonth,
        int? anchorMonth, DateOnly startDate, DateOnly? endDate, Category category, bool isSubscription, DateOnly? commitmentEndDate)
    {
        if (!isSubscription && commitmentEndDate is not null)
        {
            throw new McpException("commitmentEndDate is for a subscription only.");
        }

        return new RecurringLine(id, kind, name, amount, frequency, dayOfMonth, startDate, anchorMonth, endDate, category,
            isSubscription ? new SubscriptionInfo(commitmentEndDate) : null);
    }

    /// <summary>What a tool of the plan changes, for its messages.</summary>
    private sealed record PlanItem(string Name);

    private static readonly PlanItem Budgets = new("budget");
    private static readonly PlanItem Lines = new("recurring line");

    /// <summary>
    /// The skeleton of the tools of the plan: the whole plan read (every budget, every line, ended ones included), the
    /// domain deciding what to save, then the save, then the plan of the month shown read back through the path of
    /// get_plan. Nothing is written when the domain refuses; a name the database refuses (taken meanwhile) becomes the
    /// same tool error as one the domain refuses.
    /// </summary>
    /// <param name="valueRule">What a value of this tool must be, for the error of a value refused by the domain.</param>
    /// <param name="decide">The rules of the plan applied, giving the save to run.</param>
    private static async Task<CallToolResult> DecideSaveAndReadAsync(
        FinancePlanReader reader,
        string month,
        PlanItem item,
        string valueRule,
        Func<FinancePlan, Func<Task>> decide,
        CancellationToken cancellationToken)
    {
        var yearMonth = MonthArgument.Parse(month);
        var plan = await reader.LoadPlanAsync(cancellationToken);

        Func<Task> save;
        try
        {
            save = decide(plan);
        }
        catch (ArgumentException error)
        {
            throw Refused(error, item, valueRule);
        }

        try
        {
            await save();
        }
        catch (NameTakenException error)
        {
            // Only this one: any other failure of the save is a server error, never a message about the input.
            throw Refused(error, item, valueRule);
        }

        return await FinanceDataTools.PlanAsync(reader, yearMonth, cancellationToken);
    }

    /// <summary>
    /// A rule of the plan broken by an input, as a tool error a client can read and fix: told apart by the type of the
    /// domain exception, never by a parameter name; never the value sent (the domain message and its details stay in the
    /// inner exception, for the server logs).
    /// </summary>
    private static McpException Refused(ArgumentException error, PlanItem item, string valueRule) => new(error switch
    {
        UnknownBudgetException => UnknownBudget,
        UnknownLineException => "No recurring line with this identifier in the plan.",
        NameTakenException => $"Another {item.Name} already has this name.",
        _ => valueRule,
    }, error);

    private const string BudgetNameDescription =
        "Name of the budget, such as Courses: at most 100 characters, no line break nor invisible character; not the name "
        + "of another budget.";

    private const string PlannedAmountDescription =
        "Planned amount for every month, in euros with at most two decimals, more than 0.";

    private static readonly string BudgetRule = string.Create(CultureInfo.InvariantCulture,
        $"Invalid budget: expected a name of at most {NameLimit.MaxLength} characters, with a visible one, without line break, "
        + $"control nor invisible character, and a planned amount in euros with at most two decimals, more than 0, at most {AmountLimit.Max}.");

    private const string KindDescription = "Income or Expense: the direction of the amount.";

    private const string LineNameDescription =
        "Name of the line, such as Loyer: at most 100 characters, no line break nor invisible character; not the name of "
        + "another recurring line, ended ones included.";

    private const string AmountDescription =
        "Amount of one debit or credit, in euros with at most two decimals, more than 0 (the kind gives the direction).";

    private const string FrequencyDescription = "Monthly, Quarterly or Yearly.";

    private const string DayOfMonthDescription =
        "Day of the month it falls due, 1 to 31 (29 to 31: the last day of shorter months).";

    private const string AnchorMonthDescription =
        "Month of its first occurrence in the year, 1 to 12, for a Quarterly or Yearly line; null for a Monthly one.";

    private const string StartDateDescription = "First day it runs, as YYYY-MM-DD.";

    private const string EndDateDescription =
        "Last day it runs, as YYYY-MM-DD, never before the start date; null when it has no end.";

    // Every value of the enum Category, checked by a test.
    private const string CategoryDescription =
        "One of Other, Income, Housing, Utilities, Telecom, Insurance, Transport, Health, Food, Leisure, Education, Savings, "
        + "Taxes.";

    private const string IsSubscriptionDescription = "True for a subscription (an expense only).";

    private const string CommitmentEndDateDescription =
        "End of the commitment of a subscription, as YYYY-MM-DD, or null; always null when isSubscription is false.";

    private static readonly string LineRule = string.Create(CultureInfo.InvariantCulture,
        $"Invalid recurring line: expected a name of at most {NameLimit.MaxLength} characters, with a visible one, without line "
        + $"break, control nor invisible character; an amount in euros with at most two decimals, more than 0, at most "
        + $"{AmountLimit.Max}; a day of the month from 1 to 31; an anchor month from 1 to 12 for a quarterly or yearly line "
        + $"only; an end date never before the start date; a subscription for an expense only.");

    private const string EndRule = "Invalid end date: never before the start date of the line.";
}
