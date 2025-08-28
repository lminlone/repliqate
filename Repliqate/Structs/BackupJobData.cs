namespace Repliqate.Structs;

public class BackupJobData
{
    public required DockerContainer ContainerInfo { set; get; }
    
    /// <summary>
    /// The root of the backup, this includes the id of the container
    /// </summary>
    public required string DestinationRoot { set; get; }
}