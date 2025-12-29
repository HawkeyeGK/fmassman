using fmassman.Web.Components;
using fmassman.Web.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddCircuitOptions(options => options.DetailedErrors = true);

// Register all FmAssMan services (Cosmos or file-based, ViewModels, etc.)
builder.Services.AddFmAssManServices(builder.Configuration);

var app = builder.Build();

// Initialize RoleService to load local data (or baseline) before first request
using (var scope = app.Services.CreateScope())
{
    var roleService = scope.ServiceProvider.GetRequiredService<fmassman.Shared.Services.IRoleService>();
    try
    {
        await roleService.InitializeAsync();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"CRITICAL ERROR: Failed to initialize roles. App may suffer functionality loss. {ex.Message}");
    }
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

var cultureInfo = new System.Globalization.CultureInfo("en-US");
System.Globalization.CultureInfo.DefaultThreadCurrentCulture = cultureInfo;
System.Globalization.CultureInfo.DefaultThreadCurrentUICulture = cultureInfo;

await app.RunAsync();
