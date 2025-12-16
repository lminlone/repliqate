using Grpc.Core;
using RepliqateProtos;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;
using Repliqate.Structs;

namespace Repliqate.Services;

public class IpcServiceImpl : IpcService.IpcServiceBase
{
    private readonly ILogger<IpcServiceImpl> _logger;
    private readonly DockerConnector _dockerConnector;
    private readonly ScheduleManager _scheduleManager;
    private readonly BackupJob _backupJob;
    
    private List<string> _peers = new();

    public IpcServiceImpl(ILogger<IpcServiceImpl> logger, DockerConnector dockerConnector, ScheduleManager scheduleManager, BackupJob backupJob)
    {
        _logger = logger;
        _dockerConnector = dockerConnector;
        _scheduleManager = scheduleManager;
        _backupJob = backupJob;
    }

    public override Task<PingReply> Ping(PingRequest request, ServerCallContext context)
    {
        // Limit pong size as a precaution
        var pong = request.Message.Substring(0, Math.Min(request.Message.Length, 128));
        
        // Echo back the message
        var reply = new PingReply
        {
            Message = pong
        };
        return Task.FromResult(reply);
    }

    public override Task<ConnectConfirmReply> ConnectConfirm(ConnectConfirmRequest request, ServerCallContext context)
    {
        _logger.LogInformation("IPC client {Peer} connection confirmed", context.Peer);
        return Task.FromResult(new ConnectConfirmReply());
    }

    public override Task<DisconnectConfirmReply> DisconnectConfirm(DisconnectConfirmRequest request, ServerCallContext context)
    {
        _logger.LogInformation("IPC client {Peer} gracefully disconnected", context.Peer);
        return Task.FromResult(new DisconnectConfirmReply());
    }

    public override Task<VersionReply> GetVersion(VersionRequest request, ServerCallContext context)
    {
        var reply = new VersionReply
        {
            Version = Program.GetVersionString()
        };
        return Task.FromResult(reply);
    }

    public override Task<DockerVersionReply> GetDockerVersion(DockerVersionRequest request, ServerCallContext context)
    {
        var versionInfo = _dockerConnector.GetVersionAsync();

        var reply = new DockerVersionReply()
        {
            Version = versionInfo.Result.Version,
            ApiVersion = versionInfo.Result.APIVersion
        };
        return Task.FromResult(reply);
    }

    public override async Task<BackupReply> Backup(BackupRequest request, ServerCallContext context)
    {
        var response = new BackupReply();
        
        _logger.LogInformation("Backup requested by IPC client {Peer}", context.Peer);
        
        var scheduler = _scheduleManager.GetScheduler();
        
        // Pause all scheduled jobs just to be safe, before we trigger a manual backup
        _logger.LogInformation("Pausing all scheduled jobs");
        await scheduler.PauseAll();
        
        // Container based backup trigger
        var containerNameToBackup = request.ContainerName;
        if (containerNameToBackup != "")
        {
            response = await BackupContainer(containerNameToBackup);
        }
        
        _logger.LogInformation("Resuming all scheduled jobs");
        await scheduler.ResumeAll();
            
        return await Task.FromResult(response);
    }

    private async Task<BackupReply> BackupContainer(string containerName)
    {
        DockerContainer? containerToBackup = null;
        
        // Figure out which container we are wanting to backup
        var containers = _dockerConnector.GetContainers();
        foreach (DockerContainer container in containers)
        {
            if (container.Name == containerName)
            {
                containerToBackup = container;
            }
        }

        if (containerToBackup == null)
        {
            return new BackupReply();
        }

        _logger.LogInformation("Triggering backup manually for container {ContainerName}", containerName);
            
        BackupJobData jobData = _scheduleManager.GetJobData(containerToBackup.ID);
        await _backupJob.RunBackupTask(jobData);

        return new BackupReply();
    }
}
