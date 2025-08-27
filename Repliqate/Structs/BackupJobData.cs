namespace Repliqate.Structs;

public class BackupJobData
{
    public required DockerContainer ContainerInfo { set; get; }
    public required string DestinationRoot { set; get; }
}