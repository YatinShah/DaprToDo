
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ToDoApi
{
    public class Program
    {
        public static void Main(string[] args)
        {

            var builder = WebApplication.CreateBuilder(args);
            builder.Environment.EnvironmentName = Environments.Development;
            var env = builder.Environment.EnvironmentName;

            var config = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .AddJsonFile("appsettings.json", false)
                .AddJsonFile($"appsettings.{env}.json", true)
                .Build();

            builder.Configuration.AddConfiguration(config);

            builder.Host.ConfigureLogging(logBuilder => {
                logBuilder
                .AddConfiguration(config)
                .AddConsole()
                .AddDebug();
            });

            builder.Services.AddControllers();
            builder.Services.AddDaprServices(config);
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();

            var app = builder.Build();


            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI();
            }


            DaprConfigExtension.AddDaprConfigServices(app);
            app.UseAuthorization();


            app.MapControllers();

            app.Run();
        }
    }


    public static class DaprServicesExtension
    {
        public static IServiceCollection AddDaprServices(this IServiceCollection services, IConfigurationRoot config)
        {
            services.AddHealthChecks();
            services.AddDaprSidekick(config)
            ;
            return services;
        }
    }

    public static class DaprConfigExtension
    {
        public static IApplicationBuilder AddDaprConfigServices(IApplicationBuilder app)
        {
            app.UseRouting();
            app.UseHealthChecks("/health")
            .UseEndpoints(endpoints =>
            {
                //endpoints.MapControllers();
                 //endpoints.MapHealthChecks("/health");
            });
            return app;
        }
    }
}