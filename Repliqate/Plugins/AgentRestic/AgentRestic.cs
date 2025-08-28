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
            _logger.LogError("Failed to extract bundled asset for Restic");
            return false;
        }

        string backupPath = Path.Join(jobData.DestinationRoot, jobData.ContainerInfo.GetBackupId());
        Directory.CreateDirectory(backupPath);
        
        // Then backup each volume
        foreach (var mount in jobData.ContainerInfo.Mounts)
        {
            if (mount.Type != "volume")
                continue;
            
            // Ensure that the mount source path exists first and we have access to it, otherwise we can't back it up
            if (!Directory.Exists(mount.Source))
            {
                _logger.LogError("Volume mount source path {VolumeName} ({VolumePath}) does not exist, skipping", mount.Name, mount.Source);
                continue;
            }
            
            string backupDest = Path.Join(backupPath, mount.Name);
            Directory.CreateDirectory(backupDest);
            
            string repoPath = Path.Join(backupDest, "repo");

            // Ensure that the repo exists first
            await restic.EnsureRepoExists(repoPath);
            
            _logger.LogInformation("Backing up volume {VolumePath}", mount.Name);
            var result = await restic.BackupFiles(mount.Source, repoPath, msg =>
            {
                
            });
            _logger.LogInformation("Backup done. New files: {FilesNew} | New dirs: {DirsNew}", result.FilesNew, result.DirsNew);
        }
        
        return false;
    }

    public async Task<Restic?> LoadRestic()
    {
        return new Restic(_loggerFactory.CreateLogger<Restic>(), "restic");
    }

    private string GetZippedAssetName()
    {
        return CompileAssetName() + GetZippedExtension();
    }

    private string GetBinName()
    {
        return CompileAssetName() + GetBinExtension();
    }

    private string GetZippedExtension()
    {
        if (OperatingSystem.IsWindows())
            return ".zip";
        else if (OperatingSystem.IsLinux())
            return ".bz2";
        else
            throw new ArgumentOutOfRangeException();
    }

    private string GetBinExtension()
    {
        if (OperatingSystem.IsWindows())
            return ".exe";
        else if (OperatingSystem.IsLinux())
            return "";
        else
            throw new ArgumentOutOfRangeException();
    }

    private string CompileAssetName()
    {
        string result = "restic_";
        result += BundledResticVersion;
        result += "_";
        result += CompileAssetNameSuffix();
        return result;
    }

    private string CompileAssetNameSuffix()
    {
        string result = "";

        if (OperatingSystem.IsWindows())
            result = "windows";
        else if (OperatingSystem.IsLinux())
            result = "linux";

        result += "_";

        switch (RuntimeInformation.OSArchitecture)
        {
            case Architecture.X64:
                result += "amd64";
                break;
            case Architecture.X86:
                result += "386";
                break;
            case Architecture.Arm:
                result += "arm";
                break;
            case Architecture.Arm64:
                result += "arm64";
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        return result;
    }
}