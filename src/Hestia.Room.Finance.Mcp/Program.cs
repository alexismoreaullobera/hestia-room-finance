using Hestia.Room.Finance.Infrastructure;
using Hestia.Room.Finance.Mcp.Time;
using Hestia.Room.Finance.Mcp.Tools;
using Hestia.Room.Finance.Mcp.Views;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Extensions.Apps;

var builder = WebApplication.CreateBuilder(args);

// Locally from the .NET user secrets (see README), in production from the environment: never in a committed file.
var connectionString = builder.Configuration.GetConnectionString("Finance");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "The connection string ConnectionStrings:Finance is not set. Locally, set it in the .NET user secrets (see README).");
}

builder.Services.AddFinanceInfrastructure(connectionString);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<FinanceClock>();

builder.Services.AddMcpServer()
    .WithHttpTransport(options =>
    {
        // No server-to-client requests (sampling, elicitation) needed: stateless is enough.
        options.SessionMode = HttpServerSessionMode.Stateless;
    })
    // Explicit registration: only the tools and views listed here are exposed.
    .WithTools<FinanceDataTools>()
    .WithTools<FinanceViewTools>()
    .WithTools<FinanceInputTools>()
    .WithResources(FinanceViewResources.Create())
    .WithMcpApps();

// Browser-based MCP hosts calling the server directly (e.g. the future Hestia shell) need CORS.
// Development only, restricted to the allowed local origins from configuration.
const string DevHostsCorsPolicy = "DevMcpHosts";
var devHostOrigins = builder.Configuration.GetSection("Cors:DevMcpHostOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy(DevHostsCorsPolicy, policy => policy
    .WithOrigins(devHostOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

// Created now in every environment: a missing time zone stops the startup instead of failing the first tool call.
var clock = app.Services.GetRequiredService<FinanceClock>();

if (app.Environment.IsDevelopment())
{
    app.UseCors(DevHostsCorsPolicy);

    // Development only: migrations and demo plan at startup. In production, the deployment pipeline migrates.
    var currentMonth = clock.CurrentMonth();
    var demoSeeded = await app.Services.PrepareDevelopmentDatabaseAsync(currentMonth, app.Lifetime.ApplicationStopping);
    if (demoSeeded)
    {
        app.Logger.LogInformation("Development database ready: demo plan seeded for {Month}.", currentMonth);
    }
    else
    {
        app.Logger.LogInformation("Development database ready: the existing plan is kept.");
    }
}

app.MapMcp("/mcp");

await app.RunAsync();
