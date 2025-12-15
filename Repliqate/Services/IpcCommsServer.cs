using Grpc.Core;
using RepliqateProtos;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Repliqate.Services;

public class IpcServiceImpl : IpcService.IpcServiceBase
{
    private readonly ILogger<IpcServiceImpl> _logger;
    private readonly DockerConnector _dockerConnector;
    
    private List<string> _peers = new();

    public IpcServiceImpl(ILogger<IpcServiceImpl> logger, DockerConnector dockerConnector)
    {
        _logger = logger;
        _dockerConnector = dockerConnector;
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
}
