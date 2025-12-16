using System.CommandLine;
using GrpcDotNetNamedPipes;
using RepliqateProtos;

namespace Repliqate;

public class RepliqateClientCli
{
    private IpcService.IpcServiceClient? _client = null;
    
    public void Start(string[] args)
    {
        RootCommand rootCommand = new("Repliqate CLI");

        Command cmdVersion = new("version", "Display Repliqate version");

        Option<string> optBackupContainer = new("--container")
        {
            Description = "The name of the container to backup"
        };
        Option<bool> optBackupNow = new("--now")
        {
            Description = "Backup now, bypassing schedule"
        };
        Command cmdBackup = new("backup", "Schedule a backup manually")
        {
            optBackupContainer,
            optBackupNow
        };
        cmdBackup.SetAction(pr => CmdBackup(
            pr.GetValue(optBackupNow),
            pr.GetValue(optBackupContainer)
        ));

        // Add the sub commands to the main root command
        rootCommand.Subcommands.Add(cmdVersion);
        rootCommand.Subcommands.Add(cmdBackup);
        
        ParseResult parseResult = rootCommand.Parse(args);
        
        var channel = new NamedPipeChannel(".", "repliqate");
        _client = new IpcService.IpcServiceClient(channel);
        
        // Ensure connection, be nice to the server
        ConnectConfirmReply? confirmReply = _client.ConnectConfirm(new ConnectConfirmRequest());
        if (confirmReply == null)
        {
            Console.WriteLine("Failed to connect to IPC server");
            return;
        }

        parseResult.Invoke();
        
        _client.DisconnectConfirm(new DisconnectConfirmRequest());
    }

    private void CmdBackup(bool now, string? containerName = "")
    {
        // Repliqate version
        VersionReply? repliqateVersion = _client.GetVersion(new VersionRequest());
        DockerVersionReply? dockerVersion = _client.GetDockerVersion(new DockerVersionRequest());
        
        Console.WriteLine($"Repliqate version: {repliqateVersion.Version}");
        Console.WriteLine($"Docker version: {dockerVersion.Version}");
    }
}