using Microsoft.EntityFrameworkCore;
using OrderProcessing.EmailWorker;
using OrderProcessing.EmailWorker.Configuration;
using OrderProcessing.EmailWorker.Emailing;
using OrderProcessing.EmailWorker.Messaging;
using OrderProcessing.EmailWorker.Persistence;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("EmailWorkerConnection") ?? 
    throw new InvalidOperationException( "EmailWorker database connection string is missing.");

builder.Services.AddDbContext<EmailWorkerDbContext>(
    options =>
        options.UseSqlServer(
            connectionString,
            sqlOptions =>
            {
                sqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "email");
            }));

builder.Services.AddScoped<IdempotentEmailMessageProcessor>();

builder.Services
    .AddOptions<RabbitMqOptions>()
    .Bind(
        builder.Configuration.GetSection(
            RabbitMqOptions.SectionName))
    .Validate(
        options =>
            !string.IsNullOrWhiteSpace(options.HostName),
        "RabbitMQ host name is required.")
    .Validate(
        options => options.Port > 0,
        "RabbitMQ port must be greater than zero.")
    .Validate(
        options =>
            !string.IsNullOrWhiteSpace(options.UserName),
        "RabbitMQ user name is required.")
    .Validate(
        options =>
            !string.IsNullOrWhiteSpace(options.Password),
        "RabbitMQ password is required.")
    .Validate(
        options =>
            !string.IsNullOrWhiteSpace(options.ExchangeName),
        "RabbitMQ exchange name is required.")
    .Validate(
        options =>
            !string.IsNullOrWhiteSpace(options.EmailQueueName),
        "RabbitMQ email queue name is required.")
    .Validate(
        options => options.PrefetchCount > 0,
        "RabbitMQ prefetch count must be greater than zero.")
    .ValidateOnStart();


builder.Services.AddSingleton<IEmailSender, LoggingEmailSender>();

builder.Services.AddScoped<OrderEventEmailHandler>();

builder.Services.AddHostedService<RabbitMqEmailConsumer>();

var host = builder.Build();

await host.RunAsync();