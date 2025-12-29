using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Azure.Cosmos;
using fmassman.Shared;
using fmassman.Shared.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication()
    .ConfigureServices((context, services) =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();

        // Configuration
        var configuration = context.Configuration;

        // Cosmos DB Configuration
        services.Configure<CosmosSettings>(configuration.GetSection("CosmosSettings"));

        // Register CosmosClient
        services.AddSingleton<CosmosClient>(sp =>
        {
            var connectionString = configuration.GetValue<string>("CosmosDb");
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("CosmosDb connection string is missing.");
            }
            return new CosmosClient(connectionString);
        });

        // Register Repositories/Services
        services.AddSingleton<IRosterRepository, CosmosRosterRepository>();

        services.AddSingleton<IRoleService>(sp =>
        {
            var cosmosClient = sp.GetRequiredService<CosmosClient>();
            var settings = sp.GetRequiredService<IOptions<CosmosSettings>>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<CosmosRoleService>>();
            
            // In Azure Functions, the execution root is different.
            // We expect roles.json to be in the same directory as the dlls/output
            var appRoot = context.HostingEnvironment.ContentRootPath;
            var baselinePath = Path.Combine(appRoot, "roles.json");

            return new CosmosRoleService(cosmosClient, settings, baselinePath, logger);
        });
    })
    .Build();

host.Run();
