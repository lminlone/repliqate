using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
using ICSharpCode.SharpZipLib.BZip2;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Repliqate.Structs;

namespace Repliqate.Plugins.AgentRestic;

public class AgentRestic : IAgent
{
    public const string BundledResticVersion = "0.18.0";

    private readonly ILogger<AgentRestic> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IConfiguration _appConfig;

    public AgentRestic(ILogger<AgentRestic> logger, ILoggerFactory loggerFactory, IConfiguration appConfig)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _appConfig = appConfig;
    }

    public override string GetName()
    {
        return "restic";
    }
    
    public override async Task<bool> BeginBackup(BackupJobData jobData)
    {
        _logger.LogInformation("Restic backup started for {ContainerName}", jobData.ContainerInfo.GetName());

        Restic? restic = await LoadRestic();
        if (restic == null)
        {
            _logger.LogError("Tried to load restic but failed. Error unknown");
            return false;
        }

        try
        {
            Directory.CreateDirectory(jobData.DestinationRoot);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to create destination directory {DestinationRoot}", jobData.DestinationRoot);
            return false;
        }

        var excludeVolumeNames = jobData.ContainerInfo.GetExcludedVolumes();

        if (jobData.ContainerInfo.Mounts.Count == 0)
        {
            _logger.LogInformation("No mounts found for container {ContainerName}, skipping", jobData.ContainerInfo.GetName());
            return false;
        }
        
        // Then backup each volume
        foreach (var mount in jobData.ContainerInfo.Mounts)
        {
            // Not a volume (obvious)
            if (mount.Type != "volume")
            {
                _logger.LogInformation("Mount {Name} ({Path}) is not a volume but instead a {Type}, skipping", mount.Name, mount.Source, mount.Type);
                continue;
            }
            
            // Fetch the volume from the volumes list
            var volumeInfo = jobData.ContainerInfo.GetVolume(mount.Name);
            if (volumeInfo == null)
            {
                _logger.LogWarning("Volume {VolumeName} ({VolumePath}) not found in container {ContainerName}, skipping", mount.Name, mount.Source, jobData.ContainerInfo.GetName());
                continue;
            }
            
            // Name was part of the exclusions
            if (volumeInfo.Labels.TryGetValue("repliqate.exclude", out var value))
            {
                if (value == "true")
                {
                    _logger.LogInformation("Volume {VolumeName} ({VolumePath}) excluded from backup via container label config, skipping", mount.Name, mount.Source);
                    continue;   
                }
            }
            
            // Ensure that the mount source path exists first and we have access to it, otherwise we can't back it up
            if (!Directory.Exists(mount.Source))
            {
                _logger.LogError("Volume mount source path {VolumeName} ({VolumePath}) does not exist, skipping", mount.Name, mount.Source);
                continue;
            }
            
            string backupDest = Path.Join(jobData.DestinationRoot, "volumes", mount.Name);
            
            _logger.LogInformation("Backing up {VolumeName} to {BackupDest}", mount.Name, backupDest);
            
            Directory.CreateDirectory(backupDest);
            
            // Ensure that the repo exists first
            await restic.EnsureRepoExists(backupDest);
            
            var result = await restic.BackupFiles(mount.Source, backupDest, msg =>
            {
                _logger.LogInformation("Progress: {ProgressMsg}%", (int)(msg.PercentDone * 100));
            });
            _logger.LogInformation("Backup done. New files: {FilesNew} | New dirs: {DirsNew} | Total files: {TotalFiles}", result.FilesNew, result.DirsNew, result.TotalFilesProcessed);
        }
        
        return true;
    }

    public async Task<Restic?> LoadRestic()
    {
        return new Restic(_loggerFactory.CreateLogger<Restic>(), "restic");
    }
}