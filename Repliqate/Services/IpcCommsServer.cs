using Grpc.Core;
using RepliqateProtos;
using System.Threading.Tasks;

namespace Repliqate.Services;

public class IpcServiceImpl : IpcService.IpcServiceBase
{
    public override Task<PingReply> Ping(PingRequest request, ServerCallContext context)
    {
        // Echo back the message
        var reply = new PingReply
        {
            Message = $"Pong: {request.Message}"
        };
        return Task.FromResult(reply);
    }

    public override Task<VersionReply> GetVersion(VersionRequest request, ServerCallContext context)
    {
        var reply = new VersionReply
        {
            Version = "1.0.0" // Replace with your version
        };
        return Task.FromResult(reply);
    }
}
