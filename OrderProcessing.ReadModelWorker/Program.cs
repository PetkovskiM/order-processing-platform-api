using Microsoft.Extensions.Options;
using MongoDB.Driver;
using OrderProcessing.ReadModelWorker.Configuration;
using OrderProcessing.ReadModelWorker.Persistence;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddOptions<MongoDbOptions>()
    .Bind(builder.Configuration.GetSection(MongoDbOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.ConnectionString),
        "MongoDB connection string is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.DatabaseName),
        "MongoDB database name is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.OrdersCollectionName),
        "MongoDB orders collection name is required.")
    .ValidateOnStart();

builder.Services.AddSingleton<IMongoClient>(serviceProvider =>
    {
        var options = serviceProvider.GetRequiredService<IOptions<MongoDbOptions>>().Value;

        return new MongoClient(options.ConnectionString);
    });

builder.Services.AddHostedService<MongoDbInitializer>();

builder.Services.AddSingleton<OrderReadModelStore>();

var host = builder.Build();

await host.RunAsync();