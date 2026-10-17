using Hestia.Room.Finance.Mcp.Tools;
using Hestia.Room.Finance.Mcp.Views;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Extensions.Apps;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMcpServer()
    .WithHttpTransport(options =>
    {
        // No server-to-client requests (sampling, elicitation) needed: stateless is enough.
        options.SessionMode = HttpServerSessionMode.Stateless;
    })
    // Explicit registration: only the tools and views listed here are exposed.
    .WithTools<FinanceViewTools>()
    .WithResources(FinanceViewResources.Create())
    .WithMcpApps();

// Browser-based MCP hosts (ext-apps basic-host) call the server cross-origin.
// Development only, restricted to the allowed local origins from configuration.
const string DevHostsCorsPolicy = "DevMcpHosts";
var devHostOrigins = builder.Configuration.GetSection("Cors:DevMcpHostOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddPolicy(DevHostsCorsPolicy, policy => policy
    .WithOrigins(devHostOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseCors(DevHostsCorsPolicy);
}

app.MapMcp("/mcp");

app.Run();
