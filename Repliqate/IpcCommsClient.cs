using GrpcDotNetNamedPipes;
using RepliqateProtos;

namespace Repliqate;

public class IpcCommsClient
{
    public void Start()
    {
        var channel = new NamedPipeChannel(".", "repliqate");
        var client = new IpcService.IpcServiceClient(channel);

        VersionReply? response = client.GetVersion(new VersionRequest());
        
        Console.WriteLine(response.Version);
    }
}