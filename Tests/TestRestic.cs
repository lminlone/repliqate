using System.Collections;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Repliqate.Plugins.AgentRestic;
using Repliqate.Plugins.AgentRestic.CliResponseStructures;
using Repliqate.Services;
using Serilog;

namespace Tests;

[TestFixture]
public class TestRestic
{
    private IServiceProvider _serviceProvider;
    private Restic _restic;

    [SetUp]
    public void Setup()
    {
        var services = new ServiceCollection();
        
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{SourceContext}] {Message:lj}{NewLine}{Exception}")
            .CreateBootstrapLogger();
        
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Debug);
        });
        
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection() // You can add test-specific configuration here if needed
            .Build();
        services.AddSingleton<IConfiguration>(configuration);
        
        services.AddScoped<DockerConnector>();
        services.AddScoped<AgentProvider>();

        _serviceProvider = services.BuildServiceProvider();
    }

    [TearDown]
    public void TearDown()
    {
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
    
    private T GetService<T>() where T : notnull
    {
        return _serviceProvider.GetRequiredService<T>();
    }

    private async Task InitializeRestic()
    {
        var agentProvider = GetService<AgentProvider>();

        var agentRestic = agentProvider.GetAgentForMethod("restic") as AgentRestic;
        Assert.That(agentRestic, Is.Not.Null);
        
        _restic = await agentRestic.LoadRestic();
        
        Assert.That(_restic, Is.Not.Null);
    }

    [Test]
    public async Task ResticCli_VersionShouldReturnBundledVersion()
    {
        await InitializeRestic();
        
        Assert.That(_restic, Is.Not.Null);
        
        var version = await _restic.GetVersion();
        Assert.That(version, Is.Not.Null);
        Assert.That(version, Is.EqualTo(AgentRestic.BundledResticVersion), $"Restic version should be be {AgentRestic.BundledResticVersion}");
    }

    [Test]
    [TestCase("Repo/test")]
    public async Task ResticCli_TestImplementation(string repoDest)
    {
        // Kill the repo first if it exists
        if (Directory.Exists(repoDest))
            Directory.Delete(repoDest, true);
        if (Directory.Exists("TestFiles"))
            Directory.Delete("TestFiles", true);
        
        Directory.CreateDirectory("TestFiles");
        File.WriteAllText(Path.Join("TestFiles", "test1.txt"), "Hello World");
        
        await InitializeRestic();

        if (!await _restic.RepoExists(repoDest))
        {
            await _restic.InitRepo(repoDest);
        }

        var repoExists = await _restic.RepoExists(repoDest);
        Assert.That(repoExists, Is.True);
        
        DateTime firstBackupTime = DateTime.Now.AddMonths(-1);
        List<string> extraArgs = new() { "--time", firstBackupTime.ToString("yyyy-MM-dd HH:mm:ss") };
        
        // Backup, but make it seem like it's a backup from a month ago
        var summary = await _restic.BackupFiles("TestFiles", repoDest, status =>
        {
            
        }, extraArgs);
        Assert.That(summary, Is.Not.Null);
        Assert.That(summary.FilesNew, Is.EqualTo(1));
        
        // // Make a change to the source
        File.WriteAllText(Path.Join("TestFiles", "test2.txt"), "Hello World");
        
        // Backup again
        var summary2 = await _restic.BackupFiles("TestFiles", repoDest, status =>
        {
            
        });
        Assert.That(summary2, Is.Not.Null);
        Assert.That(summary2.FilesNew, Is.EqualTo(1));
        
        // List all snapshots, make sure there's the right number of them and that the oldest one is the right time
        List<Snapshot> snapshots = await _restic.ListSnapshots(repoDest);
        Assert.That(snapshots, Is.Not.Null);
        Assert.That(snapshots.Count, Is.EqualTo(2));
        Assert.That(snapshots[0].Time, Is.EqualTo(firstBackupTime).Within(TimeSpan.FromSeconds(1)));
        
        // Now we want to run a forget command with a date policy
        var response = await _restic.ForgetSnapshotWithDurationPolicy(repoDest, "20d");
        Assert.That(response, Is.Not.Null);
        Assert.That(response.Count, Is.EqualTo(1));
        Assert.That(response[0].Remove.Count, Is.EqualTo(1)); // Make sure we forgot one snapshot
    }
}