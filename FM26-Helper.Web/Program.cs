using FM26_Helper.Web.Components;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddScoped<FM26_Helper.Shared.RosterRepository>();
builder.Services.AddScoped<FM26_Helper.Shared.Services.RoleService>(sp => 
{
    var config = sp.GetRequiredService<IConfiguration>();
    // Locate the Baseline in the bin folder (App Domain Base)
    var baseline = Path.Combine(AppContext.BaseDirectory, config["RolesBaselinePath"] ?? "roles.json");
    // Local file goes in the Content Root (Project Folder during dev)
    var local = config["RolesLocalPath"] ?? "roles_local.json";
    
    return new FM26_Helper.Shared.Services.RoleService(baseline, local);
});
builder.Services.AddTransient<FM26_Helper.Web.Models.RoleEditorViewModel>();
builder.Services.AddTransient<FM26_Helper.Web.Models.PlayerDetailsViewModel>();
builder.Services.AddTransient<FM26_Helper.Web.Models.PlayerEditorViewModel>();

var app = builder.Build();

// Initialize RoleService to load local data (or baseline) before first request
using (var scope = app.Services.CreateScope())
{
    var roleService = scope.ServiceProvider.GetRequiredService<FM26_Helper.Shared.Services.RoleService>();
    roleService.Initialize();
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

app.Run();
