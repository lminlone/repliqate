using System.IO.Pipes;
using Google.Protobuf;
using Microsoft.Extensions.Logging;
using ProtoBuf;
using RepliqateCliPipe;

namespace Repliqate.Services;

public class IpcCommsServer
{
    public static readonly string PipeName = "RepliqatePipe";
        
    ILogger<IpcCommsServer> _logger;
    
    public IpcCommsServer(ILogger<IpcCommsServer> logger)
    {
        _logger = logger;
    }
    
    public void Start()
    {
        Task.Run(StartAsync);
    }

    private async Task StartAsync()
    {
        _logger.LogInformation("Starting IPC server");

        while (true)
        {
            await MainLoop();
        }
    }

    private async Task MainLoop()
    {
        await using var pipe = new NamedPipeServerStream(PipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        await pipe.WaitForConnectionAsync();
        _logger.LogDebug("Client connected");
            
        // Read the incoming message
        var message = Envelope.Parser.ParseFrom(pipe);
        
        // Handle the message and send response
        if (message.ReqVersion != null)
        {
            var response = new Envelope
            {
                RespVersion = new RespVersion { Version = "1.0.0" } // Replace with actual version
            };
                
            var output = new CodedOutputStream(pipe, leaveOpen: true);
            response.WriteTo(output);
            output.Flush();
        }
    }
}