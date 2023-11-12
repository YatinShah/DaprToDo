
using Dapr.Client;
using Dapr.Extensions.Configuration;

using Man.Dapr.Sidekick;
using Man.Dapr.Sidekick.Threading;

namespace ToDoApi
{
    public class Program
    {
        internal const string secretStoreName = "localsecretstore";
        internal const string appDaprName = "weathertodoapi";
        public static async Task<int> Main(string[] args)
        {
            try
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

                builder.Host.ConfigureLogging(logBuilder =>
                {
                    logBuilder
                    .AddConfiguration(config)
                    .AddConsole()
                    .AddDebug();
                });

                builder.Services.AddDaprServices(config);
                builder.Services.AddEndpointsApiExplorer();
                builder.Services.AddSwaggerGen();

                //if (env == Environments.Development) //add sidecar detailing when in debug mode.
                builder.Services.ExplicitConfigForDebug(builder.Environment, config);


                var app = builder.Build();

                // retrieve the logger
                var logger = app.Services.GetRequiredService<ILogger<Program>>();

                if (app.Environment.IsDevelopment())
                {
                    app.UseSwagger();
                    app.UseSwaggerUI();
                }
                //await WaitForSidecarToStartAsync();

                //await DaprAddSecretStoreAsync(builder.Configuration, logger);

                app.AddDaprConfigServices();
                app.UseAuthorization();


                app.MapControllers();

                app.Run();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error occurred program startup.. {ex.Message}");
                return 1;
            }
            return 0;
        }

        private static async Task DaprAddSecretStoreAsync(ConfigurationManager configMgr, ILogger<Program> logger)
        {
            using var client = new DaprClientBuilder().Build();
            // Get secrets from store during startup
            var secret = await client.GetSecretAsync(secretStoreName, "secret");

            // Use secrets to setup your services
            logger.LogInformation("----- Secret from DaprClient: " + string.Join(",", secret.Select(d => d.Value)));

            // or forward them in the ConfigurationManager
            configMgr.AddDaprSecretStore(secretStoreName, client);

            logger.LogInformation("----- Secret from ConfigurationManager: " + configMgr.GetSection("secret").Value);
        }

        private static async Task WaitForSidecarToStartAsync()
        {
            // Wait for the Dapr sidecar to report healthy before attempting to use any Dapr components.
            using var client = new DaprClientBuilder().Build();
            using var tokenSource = new CancellationTokenSource(TimeSpan.FromMinutes(1));
            // use the await keyword in your service instead
            await client.WaitForSidecarAsync(tokenSource.Token);
        }
    }


    public static class DaprServicesExtension
    {
        public static IServiceCollection AddDaprServices(this IServiceCollection services, IConfigurationRoot config)
        {
            services.AddControllers()
                .AddDapr();
            services.AddHealthChecks();//.AddDaprSidecar();
            services.AddDaprClient();
            //services.AddDaprSidekick(config)
            ;
            return services;
        }

        public static IServiceCollection ExplicitConfigForDebug(this IServiceCollection services, IWebHostEnvironment env, IConfigurationRoot config)
        {
            var sidecarOptions = new DaprSidecarOptions
            {
                AppId = Program.appDaprName,
                AppPort = 5000,
                DaprGrpcPort = 50001,
                DaprHttpPort=3506,
                AppProtocol = "http",
                Enabled =false, //*************************KEY*******************
                ResourcesDirectory = Path.Combine(env.ContentRootPath, "Dapr/Components"),

                // Set the working directory to our project to allow relative paths in component yaml files
                WorkingDirectory = env.ContentRootPath,
                AllowedOrigins = "*",
                LogLevel = LogLevel.Debug.ToString(),
            };

            // Build the default Sidekick controller
            //var sidekick = new DaprSidekickBuilder().Build();

            // Start the Dapr Sidecar early in the pipeline, this will come up in the background
            //sidekick.Sidecar.Start(() => new DaprOptions { Sidecar = sidecarOptions }, DaprCancellationToken.None);

            // Add Dapr Sidekick
            services.AddDaprSidekick(config, o =>
            {
                o.Sidecar = sidecarOptions;
            });
            return services;
        }
    }

    public static class DaprConfigExtension
    {
        public static IApplicationBuilder AddDaprConfigServices(this IApplicationBuilder app)
        {
            app.UseRouting();
            app.UseCloudEvents();
            //app.UseHealthChecks("/healthz");
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapSubscribeHandler();
                //endpoints.MapControllers();
                endpoints.MapHealthChecks("/health");
            });
            return app;
        }
    }

}