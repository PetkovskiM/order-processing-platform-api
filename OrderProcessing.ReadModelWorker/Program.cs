using Microsoft.Extensions.Options;
using MongoDB.Driver;
using OrderProcessing.ReadModelWorker;
using OrderProcessing.ReadModelWorker.Configuration;
using OrderProcessing.ReadModelWorker.Messaging;
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

builder.Services
    .AddOptions<RabbitMqOptions>()
    .Bind(builder.Configuration.GetSection(RabbitMqOptions.SectionName))
    .Validate(options => !string.IsNullOrWhiteSpace(options.HostName), "RabbitMQ host is required.")
    .Validate(options => !string.IsNullOrWhiteSpace(options.ReadModelQueueName), "RabbitMQ read-model queue name is required.")
    .ValidateOnStart();


builder.Services.AddSingleton<IOrderReadModelRepository, MongoOrderReadModelRepository>();

builder.Services.AddScoped<OrderEventProjectionHandler>();

builder.Services.AddHostedService<RabbitMqReadModelConsumer>();

var host = builder.Build();

await host.RunAsync();