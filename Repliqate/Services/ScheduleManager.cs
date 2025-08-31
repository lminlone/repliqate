using System.ComponentModel;
using System.Text.Json;
using Docker.DotNet.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Quartz;
using Quartz.Impl;
using Quartz.Impl.Matchers;
using Repliqate.Factories;
using Repliqate.Plugins;
using Repliqate.Structs;
using Serilog;

namespace Repliqate.Services;

public class BackupJobMetadata
{
    public string ContainerId { get; set; }
    public string EngineUsed { get; set; }
    public DateTime LastBackupTime { get; set; }
}

/// <summary>
/// The actual job itself that is created and called into when the scheduled time ticks over.
/// </summary>
public class BackupJob : IJob
{
    private readonly ILogger<BackupJob> _logger;
    private readonly ScheduleManager _scheduleManager;
    private readonly DockerConnector _dockerConnector;
    private readonly AgentProvider _agentProvider;

    public BackupJob(ILogger<BackupJob> logger, ScheduleManager scheduleManager, DockerConnector dockerConnect, AgentProvider agentProvider)
    {
        _logger = logger;
        _scheduleManager = scheduleManager;
        _dockerConnector = dockerConnect;
        _agentProvider = agentProvider;
    }
    
    public async Task Execute(IJobExecutionContext context)
    {
        // Fetch data from the scheduler
        var jobData = _scheduleManager.GetJobData(context.JobDetail.Key.Name);
        string containerName = jobData.ContainerInfo.GetName();

        try
        {
            await InternalExecute(context, jobData);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "An error occurred during backup job for container {ContainerName}: {Exception}", containerName, e.Message);
        }
        
        _logger.LogInformation("Backup job finished for {ContainerName} (took {ElapsedMinutes}), next execution time: {Time}", containerName, context.JobRunTime.ToString(), context.NextFireTimeUtc.ToString());
    }

    private async Task InternalExecute(IJobExecutionContext context, BackupJobData jobData)
    {
        string containerName = jobData.ContainerInfo.GetName();
        
        _logger.LogInformation("Backup job started for {ContainerName}", containerName);
        
        _logger.LogInformation("Stopping container {ContainerName}", containerName);
        bool stopSuccess = await _dockerConnector.StopContainer(jobData.ContainerInfo.ID);
        if (stopSuccess)
        {
            _logger.LogInformation("Successfully stopped container {ContainerName}", containerName);

            await RunBackupTask(context, jobData);
            
            _logger.LogInformation("Starting container {ContainerName}", containerName);
            bool startSuccess = await _dockerConnector.StartContainer(jobData.ContainerInfo.ID);
            if (startSuccess)
            {
                _logger.LogInformation("Successfully started container {ContainerName}", containerName);
            }
            else
            {
                _logger.LogWarning("Failed to start container {ContainerName}, exiting job", containerName);
            }
        }
        else
        {
            _logger.LogWarning("Failed to stop container {ContainerName}, exiting job", containerName);
        }
    }

    private async Task RunBackupTask(IJobExecutionContext context, BackupJobData jobData)
    {
        _logger.LogInformation("Beginning backup for {ContainerName}", jobData.ContainerInfo.GetName());
        
        string engine = jobData.ContainerInfo.GetRepliqateEngine();
        IAgent? agent = _agentProvider.GetAgentForMethod(engine);
        if (agent == null)
        {
            _logger.LogError("No Repliqate method specified \"{RepliqateMethod}\" for container {ContainerName}, exiting job", engine, jobData.ContainerInfo.GetName());
            return;
        }

        bool success = await agent.BeginBackup(jobData);
        if (!success)
        {
            _logger.LogError("Failed to begin backup for container {ContainerName}, exiting job", jobData.ContainerInfo.GetName());
            return;
        }
        
        // Once done, write some metadata about it in JSON so we can recognise it later
        string backupMetadataPath = Path.Join(jobData.DestinationRoot, "metadata.json");
        _logger.LogInformation("Writing backup metadata to {Path}", backupMetadataPath);
        var metadata = new BackupJobMetadata
        {
            ContainerId = jobData.ContainerInfo.GetBackupId(),
            EngineUsed = agent.GetName(),
            LastBackupTime = DateTime.UtcNow
        };
        var serializedMetadata = JsonSerializer.Serialize(metadata);
        await File.WriteAllTextAsync(backupMetadataPath, serializedMetadata);
        
        _logger.LogInformation("Backup completed for {ContainerName}", jobData.ContainerInfo.GetName());
    }
}

public class ScheduleManager : BackgroundService
{
    private static readonly string BackupRootPath = "/var/repliqate";
    private static readonly Dictionary<string, string> RepliqateFilter = new(){ { DockerContainer.RepliqateLabelEnabled, "true" } };

    private readonly ILogger<ScheduleManager> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _config;
    private readonly DockerConnector _dockerConnector;
    
    private IScheduler _scheduler;
    
    // Need to keep a reference on the data we need for each job, since we cannot pass complex data directly
    private Dictionary<string, BackupJobData> _backupJobData = new();

    public ScheduleManager(ILogger<ScheduleManager> logger, IServiceProvider serviceProvider, IConfiguration config, DockerConnector dockerConnector)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _config = config;
        _dockerConnector = dockerConnector;

        _dockerConnector.OnContainerCreated += OnDockerContainerCreated;
        _dockerConnector.OnContainerDestroyed += OnDockerContainerDestroyed;
    }

    /// <summary>
    /// Called when Docker has created a new container. This happens when a stack is started (containers in the stack call
    /// this callback for each entry) or when a container is edited. This callback is for runtime adding of jobs.
    /// </summary>
    /// <param name="container"></param>
    private async void OnDockerContainerCreated(DockerContainer container)
    {
        await TryScheduleBackupJobForContainer<BackupJob>(container);
    }

    /// <summary>
    /// Called when Docker destroys a container.
    /// </summary>
    /// <param name="container">Transient data about the container</param>
    private async void OnDockerContainerDestroyed(DockerContainer container)
    {
        // Check if this is a Repliqate enabled container, return immediately if not
        if (!container.IsRepliqateEnabled())
            return;

        Quartz.JobKey jobKey = new(container.ID, "backup");
        bool deleteSuccess = await _scheduler.DeleteJob(jobKey);
        
        if (deleteSuccess)
            _logger.LogInformation("Deleted job for container {ContainerName}", container.Name);
        else
            _logger.LogWarning("Failed to delete job for container {ContainerName}", container.Name);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Scheduler starting");
        
        // Grab the Scheduler instance from the Factory
        StdSchedulerFactory factory = new StdSchedulerFactory();
        _scheduler = await factory.GetScheduler(stoppingToken);
        _scheduler.JobFactory = new JobFactory(_serviceProvider);
        await _scheduler.Start(stoppingToken);
        
        // Fetch labels from all containers from docker and schedule the initial batch of jobs that are available
        var containers = _dockerConnector.GetContainers(RepliqateFilter);
        foreach (var container in containers)
        {
            await TryScheduleBackupJobForContainer<BackupJob>(container);
        }
    }
    
    public BackupJobData GetJobData(string jobId)
    {
        return _backupJobData[jobId];
    }

    public async Task<bool> TryScheduleBackupJobForContainer<T>(DockerContainer container) where T : IJob
    {
        if (!container.IsRepliqateEnabled())
            return false;

        if (!container.ContainsMandatoryLabels(out var missingLabels))
        {
            _logger.LogError("Container {ContainerName} is missing the following mandatory labels: {MissingLabels}, cannot schedule backup", container.GetName(), string.Join(", ", missingLabels));
            return false;
        }
        
        // Validate the schedule
        var scheduleString = container.GetRepliqateSchedule();
        var schedule = ScheduleExpression.FromString(scheduleString);
        if (schedule is null)
        {
            _logger.LogError("Container {ContainerId} has an invalid repliqate.schedule label (\"{ScheduleStr}\"), cannot schedule backup", container.ID, scheduleString);
            return false;
        }
        
        _logger.LogInformation("Found container that Repliqate is scheduled on, for {ContainerName}", container.Name);
            
        await ScheduleBackupJobForContainer<BackupJob>(schedule, container);

        return true;
    }

    public async Task ScheduleBackupJobForContainer<T>(ScheduleExpression scheduleExpression, DockerContainer container) where T : IJob
    {
        string jobId = container.ID;
        string backupRootPath = Path.Join(BackupRootPath, container.GetBackupId());
        
        // Amend the data into the banks for the job to pick up
        _backupJobData[jobId] = new BackupJobData
        {
            ContainerInfo = container,
            DestinationRoot = backupRootPath
        };
        
        var cronStr = scheduleExpression.ToCronString();
        ITrigger trigger = TriggerBuilder.Create()
            .WithIdentity($"trigger-{jobId}", "backup")
            .StartNow()
            .WithCronSchedule(cronStr)
            .Build();
        
        DateTimeOffset? nextExecuteTime = null;
        
        // Check to make sure this isn't already scheduled, if it is then attempt to reschedule. We don't really need to do this
        // considering containers are not "updated" but instead destroyed and created.
        var groupMatcher = GroupMatcher<JobKey>.GroupEquals("backup");
        var existingJobs = await _scheduler.GetJobKeys(groupMatcher);
        var jobKey = new JobKey(container.ID, "backup");
        if (existingJobs.Contains(jobKey))
        {
            nextExecuteTime = await _scheduler.RescheduleJob(trigger.Key, trigger);
        }
        else
        {
            IJobDetail job = JobBuilder.Create<T>()
                .WithIdentity(jobId, "backup")
                .Build();
        
            nextExecuteTime = await _scheduler.ScheduleJob(job, trigger);
        }

        _logger.LogInformation("Backup job assigned to {ContainerName} with the next execute time scheduled for {NextTime}", container.Name, nextExecuteTime?.ToLocalTime().ToString());
    }
}