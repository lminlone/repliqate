using Repliqate.Structs;

namespace Repliqate.Plugins;

public class IAgent
{
    public virtual string GetName()
    {
        return "";
    }
    
    public virtual async Task<bool> BeginBackup(BackupJobData jobData)
    {
        return false;
    }
}