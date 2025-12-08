using System.IO.Pipes;
using Google.Protobuf;
using ProtoBuf;
using Repliqate.Services;
using RepliqateCliPipe;

namespace Repliqate;

public class IpcCommsClient
{
    private readonly NamedPipeClientStream _pipe = new (".", IpcCommsServer.PipeName, PipeDirection.InOut);
    
    public void Connect()
    {
        try
        {
            _pipe.Connect();

            var req = new Envelope { ReqVersion = new ReqVersion() };
        
            // Write without disposing the stream
            var output = new CodedOutputStream(_pipe, leaveOpen: true);
            req.WriteTo(output);
            output.Flush();
            
            var resp = Envelope.Parser.ParseFrom(_pipe);
            if (resp.RespVersion != null)
            {
                Console.WriteLine("Repliqate version: " + resp.RespVersion.Version);
            }
        }
        catch (IOException e)
        {
            Console.WriteLine(e.ToString());
        }
    }

    public void SendMessage<TMessage, TResponse>(TMessage message)
    {
        
    }
}