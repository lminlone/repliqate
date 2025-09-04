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

public abstract class JsonParser
{
    public static JsonSerializerOptions GeneralOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };
    
    public abstract object? ParseStdOut(string s);
    public abstract object? ParseStdErr(string s);

    public object? MarshalJson(string s, Dictionary<string, Type> parseDict)
    {
        // First have to deserialize to base class so we can determine what the message is to be able to
        // marshal into the right class.
        var json = JsonSerializer.Deserialize<ResponseHeader>(s, GeneralOptions);
        if (json is null)
            return null;
        
        if (!parseDict.TryGetValue(json.MessageType, out var transformType))
        {
            return null;
        }
        
        return (ResponseHeader)JsonSerializer.Deserialize(s, transformType, GeneralOptions);
    }
}

public class JsonParserMarshal : JsonParser
{
    private readonly Dictionary<string, Type> _parseDict;

    public JsonParserMarshal(Dictionary<string, Type> parseDict)
    {
        _parseDict = parseDict;
    }

    public override object? ParseStdOut(string s)
    {
        return MarshalJson(s, _parseDict);
    }

    public override object? ParseStdErr(string s)
    {
        return MarshalJson(s, _parseDict);
    }
}

public class JsonParserCmdResponseForgetGroup : JsonParserMarshal
{
    public JsonParserCmdResponseForgetGroup() : base(new()) {}
    
    public JsonParserCmdResponseForgetGroup(Dictionary<string, Type> parseDict) : base(parseDict)
    {
        parseDict = new()
        {
            { "exit_error", typeof(Error) },
        };
    }

    public override object? ParseStdOut(string s)
    {
        return JsonSerializer.Deserialize<List<ForgetGroup>>(s, GeneralOptions);
    }
}

public class JsonParserCmdResponseSnapshots : JsonParserMarshal
{
    public JsonParserCmdResponseSnapshots() : base(new()) {}
    
    public JsonParserCmdResponseSnapshots(Dictionary<string, Type> parseDict) : base(parseDict)
    {
        parseDict = new()
        {
            { "exit_error", typeof(Error) },
        };
    }

    public override object? ParseStdOut(string s)
    {
        return JsonSerializer.Deserialize<List<Snapshot>>(s, GeneralOptions);
    }
}

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

        var jsonParser = new JsonParserMarshal(new()
        {
            { "exit_error", typeof(Error) },
            { "version", typeof(ResticVersion) },
        });
        
        var result = await Execute(["version"], jsonParser, (msg) =>
        {
            if (msg is ResticVersion versionResponse)
            {
                version = versionResponse.Version;
            }
        }, ReportError);

        return version;
    }

    public async Task<bool> RepoExists(string repoPath)
    {
        string repoPathAbs = Path.GetFullPath(repoPath);
        
        bool result = false;

        var jsonParser = new JsonParserMarshal(new()
        {
            { "exit_error", typeof(Error) },
            { "summary", typeof(CheckSummary) },
        });
        
        // Make sure the directory at least exists
        if (Directory.Exists(repoPathAbs))
        {
            // Check check on the repo to make sure it's legit. If the execution came out on stdout then we're ok :)
            var exitCode = await Execute(["check", "-r", repoPathAbs, "--insecure-no-password"], jsonParser, msg =>
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

    public async Task<Initialized> InitRepo(string repoPath)
    {
        string repoPathAbs = Path.GetFullPath(repoPath);
        
        Initialized result = new();

        var jsonParser = new JsonParserMarshal(new()
        {
            { "exit_error", typeof(Error) },
            { "initialized", typeof(Initialized) },
        });
        
        await Execute(["init", "-r", repoPathAbs, "--insecure-no-password"], jsonParser, (msg) =>
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
        return await BackupFiles(from, repoPath, statusCallback, new());
    }

    public async Task<BackupSummary> BackupFiles(string from, string repoPath, Action<BackupStatus> statusCallback, List<string> extraArgs)
    {
        var jsonParser = new JsonParserMarshal(new()
        {
            { "exit_error", typeof(Error) },
            { "status", typeof(BackupStatus) },
            { "summary", typeof(BackupSummary) },
        });
        
        // Need to ensure we operate within the root directory of the backup path, otherwise the repo will inherit
        // parent paths (I don't make the rules).
        _cwd = Path.GetFullPath(from);
        string repoPathAbs = Path.GetFullPath(repoPath);

        BackupSummary finalSummary = new();

        var args = new List<string> { "-r", repoPathAbs, "backup", ".", "--insecure-no-password" };
        args.AddRange(extraArgs);
        
        await Execute(args.ToArray(), jsonParser, msg =>
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

    public async Task<List<Snapshot>> ListSnapshots(string repoPath)
    {
        string repoPathAbs = Path.GetFullPath(repoPath);
        
        List<Snapshot> result = new();
        
        var jsonParser = new JsonParserCmdResponseSnapshots();
        var t = await Execute(["-r", repoPathAbs, "snapshots", "--insecure-no-password"], jsonParser, (msg) =>
        {
            if (msg is List<Snapshot> response)
            {
                result = response;
            }
        }, ReportError);

        return result;
    }

    public async Task<Snapshot> ListFilesInSnapshot(string repoPath, string snapshot = "latest")
    {
        string repoPathAbs = Path.GetFullPath(repoPath);
        
        Snapshot result = new();
        
        var jsonParser = new JsonParserMarshal(new()
        {
            { "exit_error", typeof(Error) },
            { "snapshot", typeof(Snapshot) },
        });
        
        await Execute(["-r", repoPathAbs, "ls", "--insecure-no-password"], jsonParser, (msg) =>
        {
            if (msg is Snapshot response)
            {
                result = response;
            }
        }, ReportError);

        return result;
    }

    public async Task<List<ForgetGroup>> ForgetSnapshotWithDurationPolicy(string repoPath, string policy)
    {
        string repoPathAbs = Path.GetFullPath(repoPath);
        
        List<ForgetGroup> result = new();
        
        var jsonParser = new JsonParserCmdResponseForgetGroup();
        var t = await Execute(["-r", repoPathAbs, "forget", "--keep-within", policy, "--insecure-no-password"], jsonParser, (msg) =>
        {
            if (msg is List<ForgetGroup> forgottenSnapshots)
            {
                result = forgottenSnapshots;
            }
        }, ReportError);

        return result;
    }
    
    public async Task<int> Execute(string[] args, JsonParser parserInstance, Action<object?> stdOutCallback, Action<object?>? stdErrCallback = null)
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
                    var parsedOut = parserInstance.ParseStdOut(stdOut.Text);
                    stdOutCallback.Invoke(parsedOut!);
                    break;
                case StandardErrorCommandEvent stdErr:
                    var parsedErr = parserInstance.ParseStdErr(stdErr.Text);
                    stdErrCallback?.Invoke(parsedErr!);
                    break;
                case ExitedCommandEvent exited:
                    return exited.ExitCode;
                    break;
            }
        }

        return 1000;
    }

    private void ReportError(object? err)
    {
        if (err is Error error)
        {
            _logger.LogError("Restic command failed: {ErrorMessage}", error.Message);
        }
        else
        {
            _logger.LogError("Restic command failed: {ErrorMessage}", err?.ToString());
        }
    }
}