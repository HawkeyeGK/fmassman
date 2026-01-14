using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Azure.Cosmos;
using Azure.Storage.Blobs;
using fmassman.Shared;
using fmassman.Shared.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using System.Text.Json;

var host = new HostBuilder()
    .ConfigureFunctionsWebApplication(builder =>
    {
        // Configure JSON serialization to be case-insensitive
        // This ensures camelCase JSON from client properly deserializes into PascalCase C# properties
        builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
        {
            options.SerializerOptions.PropertyNameCaseInsensitive = true;
        });
    })
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

        // Register BlobServiceClient
        services.AddSingleton<BlobServiceClient>(sp =>
        {
            var connectionString = configuration.GetValue<string>("BlobStorage");
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("BlobStorage connection string is missing from configuration.");
            }
            return new BlobServiceClient(connectionString);
        });

        // Register HttpClientFactory for OpenAI
        services.AddHttpClient("OpenAI", client =>
        {
            var openAiKey = Environment.GetEnvironmentVariable("OpenAiKey");
            if (!string.IsNullOrEmpty(openAiKey))
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {openAiKey}");
            }
        });

        // Register HttpClientFactory for Miro Auth
        services.AddHttpClient("MiroAuth", client =>
        {
            client.BaseAddress = new Uri("https://api.miro.com/");
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        // Register Repositories/Services
        // Register Repositories/Services
        services.AddSingleton<IRosterRepository, fmassman.Api.Repositories.CosmosRosterRepository>();
        services.AddScoped<fmassman.Shared.ITacticRepository, fmassman.Api.Repositories.CosmosTacticRepository>();
        services.AddScoped<ITagRepository, fmassman.Api.Repositories.CosmosTagRepository>();
        services.AddScoped<fmassman.Shared.Interfaces.IPositionRepository, fmassman.Api.Repositories.CosmosPositionRepository>();
        services.AddScoped<fmassman.Shared.Interfaces.ISettingsRepository, fmassman.Api.Repositories.CosmosSettingsRepository>();
        
        services.AddScoped<PlayerAnalyzer>(); // Bugfix: Register Analyzer

        services.AddSingleton<IRoleService>(sp =>
        {
            var cosmosClient = sp.GetRequiredService<CosmosClient>();
            var settings = sp.GetRequiredService<IOptions<CosmosSettings>>();
            var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<fmassman.Api.Services.CosmosRoleService>>();
            
            // In Azure Functions, the execution root is different.
            // We expect roles.json to be in the same directory as the dlls/output
            var appRoot = context.HostingEnvironment.ContentRootPath;
            var baselinePath = Path.Combine(appRoot, "roles.json");

            return new fmassman.Api.Services.CosmosRoleService(cosmosClient, settings, baselinePath, logger);
        });
    })
    .Build();

host.Run();
