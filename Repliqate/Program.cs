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
        
        // Get version and git commit info
        var assembly = Assembly.GetExecutingAssembly();
        var version = assembly.GetName().Version?.ToString(3) ?? "unknown";
        var gitCommit = "unknown";
        
        // Extract git commit from informational version if present
        var infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (infoVersion != null && infoVersion.Contains('+'))
        {
            gitCommit = infoVersion.Split('+')[1];
        }
        
        Log.Information("Starting Repliqate v{Version} (Commit: {GitCommit})", version, gitCommit);
        
        using IHost host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                    .AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true)
                    .AddEnvironmentVariables();
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
        
        IConfiguration appConfig = host.Services.GetRequiredService<IConfiguration>();
        
        // Ensure that we have a configured backup path
        string backupRootPath = appConfig.GetValue<string>("BACKUP_ROOT_PATH", string.Empty);
        Log.Information("Backup root path set to {BackupRootPath}", backupRootPath);
        // Directory.CreateDirectory(backupRootPath);
        
        string dockerSocketPath = appConfig.GetValue<string>("DOCKER_SOCKET_PATH", string.Empty);
        Log.Information("Docker socket path set to {DockerSocketPath}", dockerSocketPath);

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