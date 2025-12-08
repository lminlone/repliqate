using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Quartz;
using Repliqate;
using Repliqate.Services;
using Serilog;
using Serilog.Core;
using Serilog.Events;

class Program
{
    public static readonly string TimeStampFormat = "dd-MM-yyyy HH:mm:ss";
    
    static async Task Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console(outputTemplate: "[{Timestamp:" + TimeStampFormat + "} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
            .CreateBootstrapLogger();
        
        Mutex mutex = new System.Threading.Mutex(false, @"Global\RepliqateMutex");
        try
        {
            if (mutex.WaitOne(0, false))
            {
                Log.Logger.Information("Service not running, starting service");
                await RunService(args);
            }
        }
        finally
        {
            await RunCli(args);
        }
    }

    private static async Task RunService(string[] args)
    {
        DotNetEnv.Env.Load();
        
        Log.Information("Starting Repliqate {Version}", GetVersionString());

        LoggingLevelSwitch serilogLogLevel = new LoggingLevelSwitch(LogEventLevel.Information);
        
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
                services.AddSingleton<IpcCommsServer>();
                services.AddHostedService(provider => provider.GetRequiredService<ScheduleManager>());
                services.AddHostedService(provider => provider.GetRequiredService<DockerConnector>());
            })
            .UseSerilog((context, services, loggerConfiguration) =>
            {
                // Pull out the log level from environment variables, fall back to information if not available
                string logLevel = context.Configuration["LOG_LEVEL"] ?? "Information";
                serilogLogLevel = new LoggingLevelSwitch(
                    Enum.TryParse<LogEventLevel>(logLevel, true, out var level) ? level : LogEventLevel.Information
                );
                
                loggerConfiguration
                    .ReadFrom.Configuration(context.Configuration)
                    .ReadFrom.Services(services)
                    .WriteTo.Console(outputTemplate: "[{Timestamp:" + TimeStampFormat +
                                                     "} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
                    .MinimumLevel.ControlledBy(serilogLogLevel)
                    .Enrich.FromLogContext();
            })
            .Build();
        
        IConfiguration appConfig = host.Services.GetRequiredService<IConfiguration>();
        
        Log.Information($"Log level: {serilogLogLevel.MinimumLevel}");
        
        // Ensure that we have a configured backup path
        string backupRootPath = appConfig.GetValue<string>("BACKUP_ROOT_PATH", string.Empty);
        Log.Information("Backup root path set to {BackupRootPath}", backupRootPath);
        // Directory.CreateDirectory(backupRootPath);

        DockerConnector dockerConnector = host.Services.GetRequiredService<DockerConnector>();
        try
        {
            dockerConnector.Initialize();
        }
        catch (Exception e)
        {
            Log.Error(e.ToString());
        }
        
        IpcCommsServer ipcCommsServer = host.Services.GetRequiredService<IpcCommsServer>();
        ipcCommsServer.Start();

        await host.RunAsync();
    }

    private static async Task RunCli(string[] args)
    {
        IpcCommsClient ipcComms = new IpcCommsClient();
        ipcComms.Connect();
    }
    
    private static string GetVersionString()
    {
        // Get version and git commit info
        var assembly = Assembly.GetExecutingAssembly();
        var version = Assembly
            .GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        return version;
    }

    private static string GetGitCommit()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var gitCommit = "unknown";

        // Extract git commit from informational version if present
        var infoVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (infoVersion != null && infoVersion.Contains('+'))
        {
            gitCommit = infoVersion.Split('+')[1];
        }

        return gitCommit;
    }
}