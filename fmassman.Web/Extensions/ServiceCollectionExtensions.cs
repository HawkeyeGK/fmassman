using Microsoft.Azure.Cosmos;

namespace fmassman.Web.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddFmAssManServices(this IServiceCollection services, IConfiguration configuration)
    {
        var cosmosConn = configuration.GetConnectionString("CosmosDb");

        if (!string.IsNullOrEmpty(cosmosConn))
        {
            // Cosmos DB Implementation with Bulk Execution enabled
            services.Configure<fmassman.Shared.CosmosSettings>(configuration.GetSection("CosmosSettings"));
            services.AddSingleton(new CosmosClient(cosmosConn, new CosmosClientOptions { AllowBulkExecution = true }));

            services.AddScoped<fmassman.Shared.IRosterRepository>(sp =>
                new fmassman.Shared.CosmosRosterRepository(
                    sp.GetRequiredService<CosmosClient>(),
                    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<fmassman.Shared.CosmosSettings>>()
                ));

            services.AddScoped<fmassman.Shared.Services.IRoleService>(sp =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                var baseline = Path.Combine(AppContext.BaseDirectory, config["RolesBaselinePath"] ?? "roles.json");
                return new fmassman.Shared.Services.CosmosRoleService(
                    sp.GetRequiredService<CosmosClient>(),
                    sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<fmassman.Shared.CosmosSettings>>(),
                    baseline
                );
            });
        }
        else
        {
            // File-based Implementation (legacy/dev)
            services.AddScoped<fmassman.Shared.IRosterRepository>(sp =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                var rosterPath = config["RosterFilePath"] ?? "roster_data.json";
                return new fmassman.Shared.RosterRepository(rosterPath);
            });

            services.AddScoped<fmassman.Shared.Services.IRoleService>(sp =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                // Locate the Baseline in the bin folder (App Domain Base)
                var baseline = Path.Combine(AppContext.BaseDirectory, config["RolesBaselinePath"] ?? "roles.json");
                // Local file goes in the Content Root (Project Folder during dev)
                var local = config["RolesLocalPath"] ?? "roles_local.json";

                return new fmassman.Shared.Services.RoleService(baseline, local);
            });
        }

        // ViewModels
        services.AddTransient<fmassman.Web.Models.RoleEditorViewModel>();
        services.AddTransient<fmassman.Web.Models.PlayerDetailsViewModel>();
        services.AddTransient<fmassman.Web.Models.PlayerEditorViewModel>();

        return services;
    }
}
