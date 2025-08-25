using Docker.DotNet.Models;

namespace Repliqate;

/// <summary>
/// Acts as a wrapper around Docker inspection info.
/// </summary>
public class DockerContainer
{
    public const string RepliqateLabelPrefix = "repliqate.";
    public const string RepliqateLabelBackupId = RepliqateLabelPrefix + "backup_id";
    public const string RepliqateLabelEngine = RepliqateLabelPrefix + "engine";
    public const string RepliqateLabelSchedule = RepliqateLabelPrefix + "schedule";
    public const string RepliqateLabelEnabled = RepliqateLabelPrefix + "enabled";
    
    private readonly ContainerInspectResponse _dockerContainerData;
    
    public string ID => _dockerContainerData.ID;
    public Config Config => _dockerContainerData.Config;
    public string Name => _dockerContainerData.Name;

    private readonly List<string> _mandatoryLabels = new()
    {
        RepliqateLabelSchedule,
        RepliqateLabelEngine,
        RepliqateLabelBackupId,
    };

    public DockerContainer(ContainerInspectResponse dockerContainerData)
    {
        _dockerContainerData = dockerContainerData;
    }

    public bool ContainsMandatoryLabels(out List<string> missingLabels)
    {
        missingLabels = new();
        
        
        foreach (var label in _mandatoryLabels)
        {
            if (!_dockerContainerData.Config.Labels.TryGetValue(label, out var value))
            {
                missingLabels.Add(label);
            }
        }
        
        return missingLabels.Count == 0;
    }
    
    public bool IsRepliqateEnabled()
    {
        return _dockerContainerData.Config.Labels.TryGetValue(RepliqateLabelEnabled, out var repliqateEnabled) && repliqateEnabled == "true";
    }

    public string GetName()
    {
        return _dockerContainerData.Name;
    }
    
    public string GetRepliqateSchedule()
    {
        return _dockerContainerData.Config.Labels[RepliqateLabelSchedule];
    }

    public string GetRepliqateEngine()
    {
        return _dockerContainerData.Config.Labels[RepliqateLabelEngine];
    }

    public string GetBackupId()
    {
        return _dockerContainerData.Config.Labels[RepliqateLabelBackupId];
    }
}