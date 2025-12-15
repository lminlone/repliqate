using GrpcDotNetNamedPipes;
using RepliqateProtos;

namespace Repliqate;

public class RepliqateClientCli
{
    public void Start(string[] args)
    {
        var channel = new NamedPipeChannel(".", "repliqate");
        var client = new IpcService.IpcServiceClient(channel);
        
        // Ensure connection, be nice to the server
        ConnectConfirmReply? confirmReply = client.ConnectConfirm(new ConnectConfirmRequest());
        if (confirmReply == null)
        {
            Console.WriteLine("Failed to connect to IPC server");
            return;
        }

        // Repliqate version
        VersionReply? repliqateVersion = client.GetVersion(new VersionRequest());
        DockerVersionReply? dockerVersion = client.GetDockerVersion(new DockerVersionRequest());
        
        Console.WriteLine($"Repliqate version: {repliqateVersion.Version}");
        Console.WriteLine($"Docker version: {dockerVersion.Version}");
        
        client.DisconnectConfirm(new DisconnectConfirmRequest());
    }
}