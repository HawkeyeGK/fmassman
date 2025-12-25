using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Azure.Cosmos;
using System;
using DotNetEnv;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        // Load .env file if it exists (for local development)
        Env.TraversePath().Load();

        // Register CosmosClient
        services.AddSingleton(s =>
        {
            string connectionString = Environment.GetEnvironmentVariable("CosmosDb") 
                                   ?? Environment.GetEnvironmentVariable("CosmosDbConnectionString");
            
            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException("CosmosDb connection string not found in environment variables.");
            }

            return new CosmosClient(connectionString, new CosmosClientOptions
            {
                SerializerOptions = new CosmosSerializationOptions
                {
                    PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                }
            });
        });

        // Register HttpClient for OpenAI
        services.AddHttpClient("OpenAI", client =>
        {
            string apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException("OPENAI_API_KEY not found in environment variables.");
            }

            client.Timeout = TimeSpan.FromMinutes(2);
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        });
    })
    .Build();

host.Run();
