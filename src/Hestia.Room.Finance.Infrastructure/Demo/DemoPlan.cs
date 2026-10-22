using Hestia.Room.Finance.Domain;

namespace Hestia.Room.Finance.Infrastructure.Demo;

/// <summary>
/// A realistic but entirely fictitious monthly plan, for the quickstart and the demos of the public repository.
/// Built relative to the current month (passed in: no clock), so that the demo always shows the current month,
/// with actuals for the previous months.
/// </summary>
public sealed record DemoPlan(
    IReadOnlyList<RecurringLine> Lines,
    IReadOnlyList<Budget> Budgets,
    IReadOnlyList<MonthPlan> MonthPlans,
    IReadOnlyList<BudgetActual> BudgetActuals,
    IReadOnlyList<RecurringOverride> Overrides)
{
    public static DemoPlan For(YearMonth currentMonth)
    {
        var start = currentMonth.AddMonths(-12).FirstDay;

        var salary = Line(FlowKind.Income, "Salaire", 2250m, day: 28, start, Category.Income);
        var rent = Line(FlowKind.Expense, "Loyer", 720m, day: 5, start, Category.Housing);
        var electricity = Line(FlowKind.Expense, "Électricité", 68m, day: 12, start, Category.Utilities);
        var transport = Line(FlowKind.Expense, "Abonnement transports", 86.40m, day: 1, start, Category.Transport);
        var savings = Line(FlowKind.Expense, "Virement épargne", 150m, day: 29, start, Category.Savings);
        var internet = Subscription("Box internet", 29.99m, day: 8, start, Category.Telecom);
        var mobile = Subscription("Forfait mobile", 12.99m, day: 8, start, Category.Telecom,
            commitmentEnd: currentMonth.AddMonths(6).LastDay);
        var streaming = Subscription("Streaming vidéo", 13.49m, day: 20, start, Category.Leisure);
        var gym = Subscription("Salle de sport", 34.90m, day: 3, start, Category.Leisure,
            commitmentEnd: currentMonth.AddMonths(2).LastDay);
        var carInsurance = new RecurringLine(Guid.CreateVersion7(), FlowKind.Expense, "Assurance auto", 165m,
            Frequency.Quarterly, dayOfMonth: 15, start, anchorMonth: currentMonth.Month, category: Category.Insurance);
        var homeInsurance = new RecurringLine(Guid.CreateVersion7(), FlowKind.Expense, "Assurance habitation", 180m,
            Frequency.Yearly, dayOfMonth: 10, start, anchorMonth: currentMonth.AddMonths(-4).Month, category: Category.Insurance);

        var groceries = new Budget(Guid.CreateVersion7(), "Courses", 320m);
        var outings = new Budget(Guid.CreateVersion7(), "Sorties", 120m);
        var fuel = new Budget(Guid.CreateVersion7(), "Carburant", 90m);

        var oneMonthAgo = currentMonth.AddMonths(-1);
        var twoMonthsAgo = currentMonth.AddMonths(-2);
        var threeMonthsAgo = currentMonth.AddMonths(-3);

        return new DemoPlan(
            [salary, rent, electricity, transport, savings, internet, mobile, streaming, gym, carInsurance, homeInsurance],
            [groceries, outings, fuel],
            // The current month starts with a small cushion: not enough to avoid the alert, so the demo shows it.
            [new MonthPlan(currentMonth, 400m)],
            [
                new BudgetActual(groceries.Id, threeMonthsAgo, 301.20m),
                new BudgetActual(outings.Id, threeMonthsAgo, 96.50m),
                new BudgetActual(fuel.Id, threeMonthsAgo, 88.10m),
                new BudgetActual(groceries.Id, twoMonthsAgo, 347.85m),
                new BudgetActual(outings.Id, twoMonthsAgo, 152m),
                new BudgetActual(fuel.Id, twoMonthsAgo, 79.40m),
                new BudgetActual(groceries.Id, oneMonthAgo, 318.60m),
                new BudgetActual(outings.Id, oneMonthAgo, 64.90m),
                new BudgetActual(fuel.Id, oneMonthAgo, 95.75m),
            ],
            [
                // A heavier electricity bill one month, and the streaming paused another month.
                RecurringOverride.WithAmount(electricity.Id, twoMonthsAgo, 82.40m),
                RecurringOverride.Skipped(streaming.Id, oneMonthAgo),
            ]);
    }

    private static RecurringLine Line(FlowKind kind, string name, decimal amount, int day, DateOnly start, Category category) =>
        new(Guid.CreateVersion7(), kind, name, amount, Frequency.Monthly, day, start, category: category);

    private static RecurringLine Subscription(string name, decimal amount, int day, DateOnly start, Category category, DateOnly? commitmentEnd = null) =>
        new(Guid.CreateVersion7(), FlowKind.Expense, name, amount, Frequency.Monthly, day, start, category: category,
            subscription: new SubscriptionInfo(commitmentEnd));
}
