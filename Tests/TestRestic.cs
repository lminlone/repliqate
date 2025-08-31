using System.Collections;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Repliqate.Plugins.AgentRestic;
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

    private async void InitializeRestic()
    {
        var agentProvider = GetService<AgentProvider>();

        var agentRestic = agentProvider.GetAgentForMethod("restic") as AgentRestic;
        Assert.That(agentRestic, Is.Not.Null);
        
        _restic = await agentRestic.LoadRestic();
        
        Assert.That(_restic, Is.Not.Null);
    }

    [Test]
    public async Task UpackRestic()
    {
        InitializeRestic();
        
        Assert.That(_restic, Is.Not.Null);
        
        var version = await _restic.GetVersion();
        Assert.That(version, Is.Not.Null);
        Assert.That(version, Is.EqualTo(AgentRestic.BundledResticVersion), $"Restic version confirmed to be {AgentRestic.BundledResticVersion}");
    }

    [Test]
    [TestCase("Repo/test")]
    public async Task InitRepo(string repoDest)
    {
        // Kill the repo first if it exists
        if (Directory.Exists(repoDest))
        {
            Directory.Delete(repoDest, true);
        }
        
        InitializeRestic();

        if (!await _restic.RepoExists(repoDest))
        {
            await _restic.InitRepo(repoDest);
        }

        var repoExists = await _restic.RepoExists(repoDest);
        Assert.That(repoExists, Is.True);

        var summary = await _restic.BackupFiles("TestFiles", repoDest, status =>
        {
            
        });
        Assert.That(summary, Is.Not.Null);
        Assert.That(summary.FilesNew, Is.EqualTo(1));
    }
}