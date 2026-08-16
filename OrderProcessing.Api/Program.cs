
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using OrderProcessing.Api.BackgroundJobs;
using OrderProcessing.Api.Configuration;
using OrderProcessing.Api.Data.Seeding;
using OrderProcessing.Api.Extensions;
using OrderProcessing.Api.Features.Orders.Queries.ReadModel;
using OrderProcessing.Api.Services.Auditing;
using OrderProcessing.Api.Services.Customers;
using OrderProcessing.Api.Services.Messaging;
using OrderProcessing.Api.Services.Outbox;
using OrderProcessing.Api.Services.Products;
using Serilog;

namespace OrderProcessing.Api
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Host.UseSerilog((context, services, configuration) =>
            {
                configuration
                    .ReadFrom.Configuration(context.Configuration)
                    .ReadFrom.Services(services)
                    .Enrich.FromLogContext();
            });

            // Add services to the container.

            builder.Services.AddControllers();
            builder.Services.AddCustomValidationResponse();
            // Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
            builder.Services.AddOpenApi();

            builder.Services.AddDbContext<Data.OrderProcessingDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

            builder.Services.AddScoped<ICustomerService, CustomerService>();
            builder.Services.AddScoped<IProductService, ProductService>();
            //builder.Services.AddScoped<IOrderService, OrderService>();
            builder.Services.AddScoped<IAuditService, AuditService>();
            builder.Services.AddScoped<IOutboxWriter, OutboxWriter>();

            if (!builder.Environment.IsEnvironment("Testing"))
            {
                builder.Services.AddHostedService<OutboxBackgroundService>();
            }

            builder.Services.AddMediatR(configuration =>
            {
                configuration.RegisterServicesFromAssemblyContaining<Program>();
            });

            if (builder.Environment.IsDevelopment())
            {
                builder.Services.AddScoped<DevelopmentDataSeeder>();
            }

            builder.Services
            .AddOptions<OutboxOptions>()
            .Bind(builder.Configuration.GetSection(OutboxOptions.SectionName))
            .Validate(
                options => options.BatchSize > 0,
                "Outbox batch size must be greater than zero.")
            .Validate(
                options => options.MaxRetryCount > 0,
                "Outbox maximum retry count must be greater than zero.")
            .Validate(
                 options => options.PollingIntervalSeconds > 0,
                 "Outbox polling interval must be greater than zero.")
            .ValidateOnStart();

            var rabbitMqEnabled = builder.Configuration
            .GetSection(RabbitMqOptions.SectionName)
            .GetValue<bool>(
                nameof(RabbitMqOptions.Enabled));

                    if (rabbitMqEnabled && !builder.Environment.IsEnvironment("Testing"))
                    {
                        builder.Services.AddSingleton<
                            IIntegrationEventPublisher,
                            RabbitMqIntegrationEventPublisher>();
                    }
                    else
                    {
                        builder.Services.AddSingleton<
                            IIntegrationEventPublisher,
                            LoggingIntegrationEventPublisher>();
                    }

            builder.Services.AddScoped<OutboxProcessor>();

            builder.Services
           .AddOptions<RabbitMqOptions>()
           .Bind(
               builder.Configuration.GetSection(
                   RabbitMqOptions.SectionName))
           .Validate(
               options =>
                   !options.Enabled ||
                   !string.IsNullOrWhiteSpace(
                       options.HostName),
               "RabbitMQ host name is required when RabbitMQ is enabled.")
           .Validate(
               options =>
                   !options.Enabled ||
                   options.Port > 0,
               "RabbitMQ port must be greater than zero.")
           .Validate(
               options =>
                   !options.Enabled ||
                   !string.IsNullOrWhiteSpace(
                       options.UserName),
               "RabbitMQ user name is required when RabbitMQ is enabled.")
           .Validate(
               options =>
                   !options.Enabled ||
                   !string.IsNullOrWhiteSpace(
                       options.Password),
               "RabbitMQ password is required when RabbitMQ is enabled.")
           .Validate(
               options =>
                   !options.Enabled ||
                   !string.IsNullOrWhiteSpace(
                       options.ExchangeName),
               "RabbitMQ exchange name is required when RabbitMQ is enabled.")
           .Validate(
               options =>
                   !options.Enabled ||
                   !string.IsNullOrWhiteSpace(
                       options.EmailQueueName),
               "RabbitMQ email queue name is required when RabbitMQ is enabled.")
           .ValidateOnStart();


            builder.Services
                .AddOptions<MongoDbOptions>()
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

            builder.Services.AddSingleton<IOrderReadModelReader, MongoOrderReadModelReader>();

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                await using var scope = app.Services.CreateAsyncScope();
            
                var seeder = scope.ServiceProvider
                    .GetRequiredService<DevelopmentDataSeeder>();
            
                await seeder.SeedAsync();
            }


            app.UseSerilogRequestLogging();

            app.UseGlobalExceptionHandling();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();

                app.UseSwaggerUI(options =>
                {
                    options.SwaggerEndpoint("/openapi/v1.json", "Order Processing API v1");
                });
            }

            app.UseHttpsRedirection();

            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }
}
