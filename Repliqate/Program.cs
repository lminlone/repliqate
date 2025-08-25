using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Quartz;
using Repliqate.Services;
using Serilog;
using Serilog.Events;

class Program
{
    static async Task Main(string[] args)
    {
        DotNetEnv.Env.Load();
        
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
            .CreateBootstrapLogger();
        
        Log.Information("Starting Repliqate v{Version}", Assembly.GetExecutingAssembly().GetName().Version.ToString(3));
        
        using IHost host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddEnvironmentVariables();
            })
            .ConfigureServices((context, services) =>
            {
                services.AddQuartz();
                services.AddTransient<BackupJob>();
                
                services.AddSingleton<DockerConnector>();
                services.AddSingleton<ScheduleManager>();
                services.AddSingleton<AgentProvider>();
                services.AddHostedService(provider => provider.GetRequiredService<ScheduleManager>());
                services.AddHostedService(provider => provider.GetRequiredService<DockerConnector>());
            })
            .UseSerilog((context, services, loggerConfiguration) => 
            {
                loggerConfiguration
                    .ReadFrom.Configuration(context.Configuration)
                    .ReadFrom.Services(services)
                    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
                    .MinimumLevel.Information()
                    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                    .Enrich.FromLogContext();
            })
            .Build();
        
        // Ensure that the user specified the backup root path
        IConfiguration appConfig = host.Services.GetRequiredService<IConfiguration>();
        string backupRootPath = appConfig.GetValue<string>("BACKUP_ROOT_PATH", string.Empty);
        if (backupRootPath == string.Empty)
        {
            Log.Fatal("BACKUP_ROOT_PATH environment variable not set, exiting");
            return;
        }

        DockerConnector dockerConnector = host.Services.GetRequiredService<DockerConnector>();
        try
        {
            dockerConnector.Initialize();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        
        await host.RunAsync();
    }
}