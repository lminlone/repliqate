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
            req.WriteDelimitedTo(_pipe);
            _pipe.Flush();
            
            var resp = Envelope.Parser.ParseDelimitedFrom(_pipe);
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