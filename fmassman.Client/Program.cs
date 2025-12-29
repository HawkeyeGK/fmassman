using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Logging;
using fmassman.Client;
using fmassman.Shared;
using fmassman.Shared.Services;
using fmassman.Client.Services;
using fmassman.Client.Models;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Enable detailed logging
builder.Logging.SetMinimumLevel(LogLevel.Debug);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("http://localhost:7071") });

builder.Services.AddScoped<IRosterRepository, ApiRosterService>();
builder.Services.AddScoped<IRoleService, ApiRoleService>();

builder.Services.AddTransient<RoleEditorViewModel>();
builder.Services.AddTransient<PlayerDetailsViewModel>();
builder.Services.AddTransient<PlayerEditorViewModel>();

var host = builder.Build();

try
{
    Console.WriteLine("Starting Blazor WebAssembly host...");
    await host.RunAsync();
}
catch (Exception ex)
{
    Console.Error.WriteLine($"[STARTUP ERROR] {ex.GetType().Name}: {ex.Message}");
    Console.Error.WriteLine($"[STACK TRACE] {ex.StackTrace}");
    throw;
}
