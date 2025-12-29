using fmassman.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddCircuitOptions(options => options.DetailedErrors = true);


// Conditional Service Registration
var cosmosConn = builder.Configuration.GetConnectionString("CosmosDb");

if (!string.IsNullOrEmpty(cosmosConn))
{
    // Cosmos DB Implementation
    builder.Services.Configure<fmassman.Shared.CosmosSettings>(builder.Configuration.GetSection("CosmosSettings"));
    builder.Services.AddSingleton(new Microsoft.Azure.Cosmos.CosmosClient(cosmosConn));

    builder.Services.AddScoped<fmassman.Shared.IRosterRepository>(sp =>
        new fmassman.Shared.CosmosRosterRepository(
            sp.GetRequiredService<Microsoft.Azure.Cosmos.CosmosClient>(),
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<fmassman.Shared.CosmosSettings>>()
        ));

    builder.Services.AddScoped<fmassman.Shared.Services.IRoleService>(sp =>
    {
        var config = sp.GetRequiredService<IConfiguration>();
        var baseline = Path.Combine(AppContext.BaseDirectory, config["RolesBaselinePath"] ?? "roles.json");
        return new fmassman.Shared.Services.CosmosRoleService(
            sp.GetRequiredService<Microsoft.Azure.Cosmos.CosmosClient>(),
            sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<fmassman.Shared.CosmosSettings>>(),
            baseline
        );
    });
}
else
{
    // File-based Implementation (legacy/dev)
    builder.Services.AddScoped<fmassman.Shared.IRosterRepository>(sp => 
    {
        var config = sp.GetRequiredService<IConfiguration>();
        var rosterPath = config["RosterFilePath"] ?? "roster_data.json";
        return new fmassman.Shared.RosterRepository(rosterPath);
    });

    builder.Services.AddScoped<fmassman.Shared.Services.IRoleService>(sp => 
    {
        var config = sp.GetRequiredService<IConfiguration>();
        // Locate the Baseline in the bin folder (App Domain Base)
        var baseline = Path.Combine(AppContext.BaseDirectory, config["RolesBaselinePath"] ?? "roles.json");
        // Local file goes in the Content Root (Project Folder during dev)
        var local = config["RolesLocalPath"] ?? "roles_local.json";
        
        return new fmassman.Shared.Services.RoleService(baseline, local);
    });
}
builder.Services.AddTransient<fmassman.Web.Models.RoleEditorViewModel>();
builder.Services.AddTransient<fmassman.Web.Models.PlayerDetailsViewModel>();
builder.Services.AddTransient<fmassman.Web.Models.PlayerEditorViewModel>();

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
