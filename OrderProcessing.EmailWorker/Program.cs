using OrderProcessing.EmailWorker;
using OrderProcessing.EmailWorker.Configuration;
using OrderProcessing.EmailWorker.Emailing;
using OrderProcessing.EmailWorker.Messaging;

var builder = Host.CreateApplicationBuilder(args);

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

builder.Services.AddSingleton<
    IEmailSender,
    LoggingEmailSender>();

builder.Services.AddScoped<
    OrderEventEmailHandler>();

builder.Services.AddHostedService<
    RabbitMqEmailConsumer>();

var host = builder.Build();

await host.RunAsync();