using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CliWrap;
using CliWrap.EventStream;
using Microsoft.Extensions.Logging;

namespace Repliqate.Plugins.AgentRestic;

public struct CliOutput
{
    public string Out { get; set; }
    public string Err { get; set; }
    public int ExitCode { get; set; }
    public TimeSpan RunTime { get; set; }
}

public class ResticCmdResponse
{
    public string MessageType { get; set; }
}

public class ResticCmdResponseError : ResticCmdResponse
{
    public int Code { get; set; }
    public string Message { get; set; }
}

// Cmd: init
public class ResticCmdResponseInitialized : ResticCmdResponse
{
    public string Id { get; set; }
    public string Repository { get; set; }
}

// Cmd: version
public class ResticCmdResponseVersion : ResticCmdResponse
{
    public string Version { get; set; }       // restic version
    public string GoVersion { get; set; }     // Go compile version
    public string GoOs { get; set; }          // Go OS
    public string GoArch { get; set; }        // Go architecture
}

// Cmd: ls
public class ResticCmdResponseSnapshot : ResticCmdResponse
{
    public string StructType { get; set; }                         // Always "snapshot" (deprecated)
    public DateTime Time { get; set; }                             // Timestamp of when the backup was started
    public string Parent { get; set; }                             // ID of the parent snapshot
    public string Tree { get; set; }                               // ID of the root tree blob
    public List<string> Paths { get; set; }                        // List of paths included in the backup
    public string Hostname { get; set; }                           // Hostname of the backed up machine
    public string Username { get; set; }                           // Username the backup command was run as
    public int Uid { get; set; }                                   // ID of owner
    public int Gid { get; set; }                                   // ID of group
    public List<string> Excludes { get; set; }                     // Paths/globs excluded from the backup
    public List<string> Tags { get; set; }                         // Tags for the snapshot
    public string ProgramVersion { get; set; }                     // restic version used
    public ResticCmdResponseSnapshotSumary Summary { get; set; }   // Snapshot statistics
    public string Id { get; set; }                                 // Snapshot ID
    public string ShortId { get; set; }                            // Short form of snapshot ID
}

public class ResticCmdResponseSnapshotSumary
{
    public DateTime BackupStart { get; set; }          // Time at which the backup was started
    public DateTime BackupEnd { get; set; }            // Time at which the backup was completed

    public int FilesNew { get; set; }                // Number of new files
    public int FilesChanged { get; set; }            // Number of files that changed
    public int FilesUnmodified { get; set; }         // Number of files that did not change

    public int DirsNew { get; set; }                 // Number of new directories
    public int DirsChanged { get; set; }             // Number of directories that changed
    public int DirsUnmodified { get; set; }          // Number of directories that did not change

    public int DataBlobs { get; set; }                // Number of data blobs added
    public int TreeBlobs { get; set; }                // Number of tree blobs added

    public int DataAdded { get; set; }               // Uncompressed data added (bytes)
    public int DataAddedPacked { get; set; }         // Compressed data added (bytes)

    public int TotalFilesProcessed { get; set; }     // Total number of files processed
    public int TotalBytesProcessed { get; set; }     // Total number of bytes processed
}

// Cmd: check
public class ResticCmdResponseCheckSummary : ResticCmdResponse
{
    public long NumErrors { set; get; }                    // Number of errors
    public List<string> BrokenPacks { set; get; }          // Damaged pack IDs
    public bool SuggestRepairIndex { set; get; }           // Run "restic repair index"
    public bool SuggestPrune { set; get; }                 // Run "restic prune"
}

public class ResticCmdResponseBackupStatus : ResticCmdResponse
{
    public int SecondsElapsed { set; get; }            // Time since backup started
    public int SecondsRemaining { set; get; }          // Estimated time remaining
    public float PercentDone { set; get; }              // Fraction of data backed up (bytes_done/total_bytes)
    public int TotalFiles { set; get; }                // Total number of files detected
    public int FilesDone { set; get; }                 // Files completed (backed up to repo)
    public int TotalBytes { set; get; }                // Total number of bytes in backup set
    public int BytesDone { set; get; }                 // Number of bytes completed (backed up to repo)
    public int ErrorCount { set; get; }                // Number of errors
    public List<string> CurrentFiles { set; get; }       // Files currently being backed up
}

public class ResticCmdResponseBackupSummary : ResticCmdResponse
{
    public bool DryRun { set; get; }                     // Whether the backup was a dry run

    public int FilesNew { set; get; }                  // Number of new files
    public int FilesChanged { set; get; }              // Number of files that changed
    public int FilesUnmodified { set; get; }           // Number of files that did not change

    public int DirsNew { set; get; }                   // Number of new directories
    public int DirsChanged { set; get; }               // Number of directories that changed
    public int DirsUnmodified { set; get; }            // Number of directories that did not change

    public int DataBlobs { set; get; }                  // Number of data blobs added
    public int TreeBlobs { set; get; }                  // Number of tree blobs added

    public int DataAdded { set; get; }                 // Amount of uncompressed data added (bytes)
    public int DataAddedPacked { set; get; }           // Amount of compressed data added (bytes)

    public int TotalFilesProcessed { set; get; }       // Total number of files processed
    public int TotalBytesProcessed { set; get; }       // Total number of bytes processed

    public DateTime BackupStart { set; get; }            // Time at which the backup was started
    public DateTime BackupEnd { set; get; }              // Time at which the backup was completed
    public double TotalDuration { set; get; }            // Total time it took for the operation (seconds)

    public string SnapshotId { set; get; }               // ID of the new snapshot (optional if skipped)
}

/// <summary>
/// Wraps the Restic CLI app using the restic scripting API (https://restic.readthedocs.io/en/latest/075_scripting.html)
/// </summary>
public class Restic
{
    private ILogger<Restic> _logger;
    private string _binPath;

    public event Action<ResticCmdResponse> OnDataResponse;
    
    public Restic(ILogger<Restic> logger, string binPath)
    {
        _logger = logger;
        _binPath = binPath;
    }
    
    public async Task<string> GetVersion()
    {
        var version = "";

        Dictionary<string, Type> parseDict = new()
        {
            { "exit_error", typeof(ResticCmdResponseError) },
            { "version", typeof(ResticCmdResponseVersion) },
        };
        
        var result = await Execute(["version"], parseDict, (msg) =>
        {
            if (msg is ResticCmdResponseVersion versionResponse)
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
            { "exit_error", typeof(ResticCmdResponseError) },
            { "summary", typeof(ResticCmdResponseCheckSummary) },
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

    public async Task<ResticCmdResponseInitialized> InitRepo(string location)
    {
        ResticCmdResponseInitialized result = new();

        Dictionary<string, Type> parseDict = new()
        {
            { "exit_error", typeof(ResticCmdResponseError) },
            { "initialized", typeof(ResticCmdResponseInitialized) },
        };
        
        await Execute(["init", "-r", location, "--insecure-no-password"], parseDict, (msg) =>
        {
            if (msg is ResticCmdResponseInitialized response)
            {
                result = response;
            }
        }, ReportError);

        return result;
    }

    public async Task<ResticCmdResponseBackupSummary> BackupFiles(string from, string repoPath, Action<ResticCmdResponseBackupStatus> statusCallback)
    {
        Dictionary<string, Type> parseDict = new()
        {
            { "exit_error", typeof(ResticCmdResponseError) },
            { "status", typeof(ResticCmdResponseBackupStatus) },
            { "summary", typeof(ResticCmdResponseBackupSummary) },
        };

        ResticCmdResponseBackupSummary finalSummary = new();

        await Execute(["-r", repoPath, "backup", from, "--insecure-no-password"], parseDict, msg =>
        {
            if (msg is ResticCmdResponseBackupStatus status)
            {
                statusCallback.Invoke(status);
            }
            else if (msg is ResticCmdResponseBackupSummary summary)
            {
                finalSummary = summary;
            }
        },
        err =>
        {
            
        });

        return finalSummary;
    }

    public async Task<ResticCmdResponseSnapshot> ListFilesInSnapshot(string location, string snapshot = "latest")
    {
        ResticCmdResponseSnapshot result = new();

        Dictionary<string, Type> parseDict = new()
        {
            { "exit_error", typeof(ResticCmdResponseError) },
            { "snapshot", typeof(ResticCmdResponseSnapshot) },
        };
        
        await Execute(["-r", location, "ls"], parseDict, (msg) =>
        {
            if (msg is ResticCmdResponseSnapshot response)
            {
                result = response;
            }
        }, ReportError);

        return result;
    }
    
    public async Task<int> Execute(string[] args, Dictionary<string, Type> jsonParseObjects, Action<ResticCmdResponse> stdOutCallback, Action<ResticCmdResponse>? stdErrCallback = null)
    {
        var argsWithJson = new[] { "--json" }.Concat(args).ToArray();
        var result = Cli.Wrap(_binPath).WithArguments(argsWithJson)
            .WithValidation(CommandResultValidation.None);
        
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

    private ResticCmdResponse? ParseCmdStreamToResticJson(string s, Dictionary<string, Type> jsonParseObjects)
    {
        // First have to deserialize to base class so we can determine what the message is to be able to
        // marshal into the right class.
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };
        var json = JsonSerializer.Deserialize<ResticCmdResponse>(s, options);
        if (json is null)
            return null;
        
        if (!jsonParseObjects.TryGetValue(json.MessageType, out var transformType))
        {
            return null;
        }
        
        return (ResticCmdResponse)JsonSerializer.Deserialize(s, transformType, options);
    }

    private void ReportError(ResticCmdResponse err)
    {
        if (err is ResticCmdResponseError error)
        {
            _logger.LogError("Restic command failed: {ErrorMessage}", error.Message);
        }
        else
        {
            _logger.LogError("Restic command failed: {ErrorMessage}", err.ToString());
        }
    }
}