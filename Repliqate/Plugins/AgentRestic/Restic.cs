using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CliWrap;
using CliWrap.EventStream;
using Microsoft.Extensions.Logging;
using Repliqate.Plugins.AgentRestic.CliResponseStructures;

namespace Repliqate.Plugins.AgentRestic;

/// <summary>
/// Wraps the Restic CLI app using the restic scripting API (https://restic.readthedocs.io/en/latest/075_scripting.html)
/// </summary>
public class Restic
{
    private ILogger<Restic> _logger;
    private string _binPath;
    private string _cwd;

    public event Action<ResponseHeader> OnDataResponse;
    
    public Restic(ILogger<Restic> logger, string binPath)
    {
        _logger = logger;
        _binPath = binPath;
        
        _cwd = Directory.GetCurrentDirectory();
    }
    
    public async Task<string> GetVersion()
    {
        var version = "";

        Dictionary<string, Type> parseDict = new()
        {
            { "exit_error", typeof(Error) },
            { "version", typeof(ResticVersion) },
        };
        
        var result = await Execute(["version"], parseDict, (msg) =>
        {
            if (msg is ResticVersion versionResponse)
            {
                version = versionResponse.Version;
            }
        }, ReportError);

        return version;
    }

    public async Task<bool> RepoExists(string location)
    {
        bool result = false;

        Dictionary<string, Type> parseDict = new()
        {
            { "exit_error", typeof(Error) },
            { "summary", typeof(CheckSummary) },
        };
        
        // Make sure the directory at least exists
        if (Directory.Exists(location))
        {
            // Check check on the repo to make sure it's legit. If the execution came out on stdout then we're ok :)
            var exitCode = await Execute(["check", "-r", location, "--insecure-no-password"], parseDict, msg =>
            {
                result = true;
            },
            err =>
            {
                result = false;
            });

            if (exitCode != 0)
            {
                result = false;
            }
        }

        return result;
    }

    public async Task EnsureRepoExists(string location)
    {
        if (!await RepoExists(location))
        {
            await InitRepo(location);
        }
    }

    public async Task<Initialized> InitRepo(string location)
    {
        Initialized result = new();

        Dictionary<string, Type> parseDict = new()
        {
            { "exit_error", typeof(Error) },
            { "initialized", typeof(Initialized) },
        };
        
        await Execute(["init", "-r", location, "--insecure-no-password"], parseDict, (msg) =>
        {
            if (msg is Initialized response)
            {
                result = response;
            }
        }, ReportError);

        return result;
    }

    public async Task<BackupSummary> BackupFiles(string from, string repoPath, Action<BackupStatus> statusCallback)
    {
        Dictionary<string, Type> parseDict = new()
        {
            { "exit_error", typeof(Error) },
            { "status", typeof(BackupStatus) },
            { "summary", typeof(BackupSummary) },
        };
        
        // Need to ensure we operate within the root directory of the backup path, otherwise the repo will inherit
        // parent paths (I don't make the rules).
        _cwd = Path.GetFullPath(from);
        string repoPathAbs = Path.GetFullPath(repoPath);

        BackupSummary finalSummary = new();

        await Execute(["-r", repoPathAbs, "backup", ".", "--insecure-no-password"], parseDict, msg =>
        {
            if (msg is BackupStatus status)
            {
                statusCallback.Invoke(status);
            }
            else if (msg is BackupSummary summary)
            {
                finalSummary = summary;
            }
        },
        err =>
        {
            
        });

        return finalSummary;
    }

    public async Task<Snapshot> ListFilesInSnapshot(string location, string snapshot = "latest")
    {
        Snapshot result = new();

        Dictionary<string, Type> parseDict = new()
        {
            { "exit_error", typeof(Error) },
            { "snapshot", typeof(Snapshot) },
        };
        
        await Execute(["-r", location, "ls"], parseDict, (msg) =>
        {
            if (msg is Snapshot response)
            {
                result = response;
            }
        }, ReportError);

        return result;
    }
    
    public async Task<int> Execute(string[] args, Dictionary<string, Type> jsonParseObjects, Action<ResponseHeader> stdOutCallback, Action<ResponseHeader>? stdErrCallback = null)
    {
        var argsWithJson = new[] { "--json" }.Concat(args).ToArray();
        var result = Cli.Wrap(_binPath).WithArguments(argsWithJson)
            .WithValidation(CommandResultValidation.None)
            .WithWorkingDirectory(_cwd);

        _logger.LogDebug("Executing from {Path}: {Command}", _cwd, result.ToString());
        
        await foreach (var cmdEvent in result.ListenAsync())
        {
            switch (cmdEvent)
            {
                case StartedCommandEvent started:
                    break;
                case StandardOutputCommandEvent stdOut:
                    var parsedOut = ParseCmdStreamToResticJson(stdOut.Text, jsonParseObjects);
                    stdOutCallback.Invoke(parsedOut!);
                    break;
                case StandardErrorCommandEvent stdErr:
                    var parsedErr = ParseCmdStreamToResticJson(stdErr.Text, jsonParseObjects);
                    stdErrCallback?.Invoke(parsedErr!);
                    break;
                case ExitedCommandEvent exited:
                    return exited.ExitCode;
                    break;
            }
        }

        return 1000;
    }

    private ResponseHeader? ParseCmdStreamToResticJson(string s, Dictionary<string, Type> jsonParseObjects)
    {
        // First have to deserialize to base class so we can determine what the message is to be able to
        // marshal into the right class.
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };
        var json = JsonSerializer.Deserialize<ResponseHeader>(s, options);
        if (json is null)
            return null;
        
        if (!jsonParseObjects.TryGetValue(json.MessageType, out var transformType))
        {
            return null;
        }
        
        return (ResponseHeader)JsonSerializer.Deserialize(s, transformType, options);
    }

    private void ReportError(ResponseHeader err)
    {
        if (err is Error error)
        {
            _logger.LogError("Restic command failed: {ErrorMessage}", error.Message);
        }
        else
        {
            _logger.LogError("Restic command failed: {ErrorMessage}", err.ToString());
        }
    }
}