
using Microsoft.EntityFrameworkCore;
using OrderProcessing.Api.BackgroundJobs;
using OrderProcessing.Api.Extensions;
using OrderProcessing.Api.Services.Auditing;
using OrderProcessing.Api.Services.Customers;
using OrderProcessing.Api.Services.Emailing;
using OrderProcessing.Api.Services.Orders;
using OrderProcessing.Api.Services.Products;
using Serilog;

namespace OrderProcessing.Api
{
    public class Program
    {
        public static void Main(string[] args)
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
            builder.Services.AddScoped<IOrderService, OrderService>();
            builder.Services.AddScoped<IAuditService, AuditService>();
            builder.Services.AddScoped<IEmailSender, LoggingEmailSender>();
            builder.Services.AddSingleton<IEmailQueue>(
             _ => new EmailQueue(capacity: 100));

            builder.Services.AddHostedService<EmailBackgroundService>();

            builder.Services.AddMediatR(configuration =>
            {
                configuration.RegisterServicesFromAssemblyContaining<Program>();
            });

            var app = builder.Build();

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
