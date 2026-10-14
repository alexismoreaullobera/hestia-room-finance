using Hestia.Room.Finance.Mcp.Tools;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Extensions.Apps;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMcpServer()
    .WithHttpTransport(options =>
    {
        // No server-to-client requests (sampling, elicitation) needed: stateless is enough.
        options.SessionMode = HttpServerSessionMode.Stateless;
    })
    // Explicit registration: only the tools listed here are exposed.
    .WithTools<FinanceViewTools>()
    .WithMcpApps();

var app = builder.Build();

app.MapMcp("/mcp");

app.Run();
