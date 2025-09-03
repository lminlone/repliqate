namespace Repliqate.Plugins.AgentRestic.CliResponseStructures;

public struct CliOutput
{
    public string Out { get; set; }
    public string Err { get; set; }
    public int ExitCode { get; set; }
    public TimeSpan RunTime { get; set; }
}

public class ResponseHeader
{
    public string MessageType { get; set; } = "";
}

public class Error : ResponseHeader
{
    public int Code { get; set; }
    public string Message { get; set; } = "";
}

// Cmd: init
public class Initialized : ResponseHeader
{
    public string Id { get; set; } = "";
    public string Repository { get; set; } = "";
}

/// <summary>
/// Response for the <c>version</c> command.
/// </summary>
public class ResticVersion : ResponseHeader
{
    /// <summary>
    /// The restic version.
    /// </summary>
    public string Version { get; set; } = "";

    /// <summary>
    /// The Go compile version.
    /// </summary>
    public string GoVersion { get; set; } = "";

    /// <summary>
    /// The operating system used to compile restic.
    /// </summary>
    public string GoOs { get; set; } = "";

    /// <summary>
    /// The CPU architecture used to compile restic.
    /// </summary>
    public string GoArch { get; set; } = "";
}

/// <summary>
/// Response for the <c>check</c> command.
/// </summary>
public class CheckSummary : ResponseHeader
{
    /// <summary>
    /// Number of errors found.
    /// </summary>
    public long NumErrors { set; get; }

    /// <summary>
    /// IDs of damaged packs.
    /// </summary>
    public List<string> BrokenPacks { set; get; } = new();

    /// <summary>
    /// Suggests running "restic repair index".
    /// </summary>
    public bool SuggestRepairIndex { set; get; }

    /// <summary>
    /// Suggests running "restic prune".
    /// </summary>
    public bool SuggestPrune { set; get; }
}

/// <summary>
/// Response for backup progress status updates.
/// </summary>
public class BackupStatus : ResponseHeader
{
    /// <summary>
    /// Seconds since the backup started.
    /// </summary>
    public int SecondsElapsed { set; get; }

    /// <summary>
    /// Estimated seconds remaining.
    /// </summary>
    public int SecondsRemaining { set; get; }

    /// <summary>
    /// Fraction of data backed up (bytes_done / total_bytes).
    /// </summary>
    public float PercentDone { set; get; }

    /// <summary>
    /// Total number of files detected.
    /// </summary>
    public int TotalFiles { set; get; }

    /// <summary>
    /// Number of files completed (backed up to repo).
    /// </summary>
    public int FilesDone { set; get; }

    /// <summary>
    /// Total number of bytes in the backup set.
    /// </summary>
    public int TotalBytes { set; get; }

    /// <summary>
    /// Number of bytes completed (backed up to repo).
    /// </summary>
    public int BytesDone { set; get; }

    /// <summary>
    /// Number of errors encountered.
    /// </summary>
    public int ErrorCount { set; get; }

    /// <summary>
    /// Files currently being backed up.
    /// </summary>
    public List<string> CurrentFiles { set; get; } = new();
}

/// <summary>
/// Response for the backup summary.
/// </summary>
public class BackupSummary : ResponseHeader
{
    /// <summary>
    /// Indicates whether the backup was a dry run.
    /// </summary>
    public bool DryRun { set; get; }

    /// <summary>
    /// Number of new files.
    /// </summary>
    public int FilesNew { set; get; }

    /// <summary>
    /// Number of files that changed.
    /// </summary>
    public int FilesChanged { set; get; }

    /// <summary>
    /// Number of files that did not change.
    /// </summary>
    public int FilesUnmodified { set; get; }

    /// <summary>
    /// Number of new directories.
    /// </summary>
    public int DirsNew { set; get; }

    /// <summary>
    /// Number of directories that changed.
    /// </summary>
    public int DirsChanged { set; get; }

    /// <summary>
    /// Number of directories that did not change.
    /// </summary>
    public int DirsUnmodified { set; get; }

    /// <summary>
    /// Number of data blobs added.
    /// </summary>
    public int DataBlobs { set; get; }

    /// <summary>
    /// Number of tree blobs added.
    /// </summary>
    public int TreeBlobs { set; get; }

    /// <summary>
    /// Amount of uncompressed data added (bytes).
    /// </summary>
    public int DataAdded { set; get; }

    /// <summary>
    /// Amount of compressed data added (bytes).
    /// </summary>
    public int DataAddedPacked { set; get; }

    /// <summary>
    /// Total number of files processed.
    /// </summary>
    public int TotalFilesProcessed { set; get; }

    /// <summary>
    /// Total number of bytes processed.
    /// </summary>
    public int TotalBytesProcessed { set; get; }

    /// <summary>
    /// Time at which the backup was started.
    /// </summary>
    public DateTime BackupStart { set; get; }

    /// <summary>
    /// Time at which the backup was completed.
    /// </summary>
    public DateTime BackupEnd { set; get; }

    /// <summary>
    /// Total time taken for the backup (in seconds).
    /// </summary>
    public double TotalDuration { set; get; }

    /// <summary>
    /// ID of the new snapshot (optional if skipped).
    /// </summary>
    public string SnapshotId { set; get; } = "";
}

/// <summary>
/// Describes why a snapshot is kept.
/// </summary>
public class KeepReason : ResponseHeader
{
    /// <summary>
    /// Snapshot described by this keep reason.
    /// </summary>
    public Snapshot Snapshot { get; set; } = new();

    /// <summary>
    /// Array containing descriptions of the matching criteria.
    /// </summary>
    public List<string> Matches { get; set; } = new();
}

/// <summary>
/// Represents snapshot statistics at the time the snapshot was created.
/// </summary>
public class SnapshotSummary : ResponseHeader
{
    /// <summary>
    /// Time at which the backup was started.
    /// </summary>
    public DateTime BackupStart { get; set; }

    /// <summary>
    /// Time at which the backup was completed.
    /// </summary>
    public DateTime BackupEnd { get; set; }

    /// <summary>
    /// Number of new files.
    /// </summary>
    public ulong FilesNew { get; set; }

    /// <summary>
    /// Number of files that changed.
    /// </summary>
    public ulong FilesChanged { get; set; }

    /// <summary>
    /// Number of files that did not change.
    /// </summary>
    public ulong FilesUnmodified { get; set; }

    /// <summary>
    /// Number of new directories.
    /// </summary>
    public ulong DirsNew { get; set; }

    /// <summary>
    /// Number of directories that changed.
    /// </summary>
    public ulong DirsChanged { get; set; }

    /// <summary>
    /// Number of directories that did not change.
    /// </summary>
    public ulong DirsUnmodified { get; set; }

    /// <summary>
    /// Number of data blobs added.
    /// </summary>
    public long DataBlobs { get; set; }

    /// <summary>
    /// Number of tree blobs added.
    /// </summary>
    public long TreeBlobs { get; set; }

    /// <summary>
    /// Amount of (uncompressed) data added, in bytes.
    /// </summary>
    public ulong DataAdded { get; set; }

    /// <summary>
    /// Amount of data added (after compression), in bytes.
    /// </summary>
    public ulong DataAddedPacked { get; set; }

    /// <summary>
    /// Total number of files processed.
    /// </summary>
    public ulong TotalFilesProcessed { get; set; }

    /// <summary>
    /// Total number of bytes processed.
    /// </summary>
    public ulong TotalBytesProcessed { get; set; }
}

/// <summary>
/// Represents a snapshot object.
/// </summary>
public class Snapshot : ResponseHeader
{
    /// <summary>
    /// Timestamp of when the backup was started.
    /// </summary>
    public DateTime Time { get; set; }

    /// <summary>
    /// ID of the parent snapshot.
    /// </summary>
    public string Parent { get; set; } = string.Empty;

    /// <summary>
    /// ID of the root tree blob.
    /// </summary>
    public string Tree { get; set; } = string.Empty;

    /// <summary>
    /// List of paths included in the backup.
    /// </summary>
    public List<string> Paths { get; set; } = new();

    /// <summary>
    /// Hostname of the backed up machine.
    /// </summary>
    public string Hostname { get; set; } = string.Empty;

    /// <summary>
    /// Username the backup command was run as.
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// ID of owner (UID).
    /// </summary>
    public uint Uid { get; set; }

    /// <summary>
    /// ID of group (GID).
    /// </summary>
    public uint Gid { get; set; }

    /// <summary>
    /// List of paths and globs excluded from the backup.
    /// </summary>
    public List<string> Excludes { get; set; } = new();

    /// <summary>
    /// List of tags for the snapshot in question.
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Restic version used to create the snapshot.
    /// </summary>
    public string ProgramVersion { get; set; } = string.Empty;

    /// <summary>
    /// Snapshot statistics summary.
    /// </summary>
    public SnapshotSummary Summary { get; set; } = new();

    /// <summary>
    /// Snapshot ID.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Snapshot ID in short form (deprecated).
    /// </summary>
    public string ShortId { get; set; } = string.Empty;
}

/// <summary>
/// Represents a group of snapshots with associated metadata.
/// </summary>
public class ForgetGroup : ResponseHeader
{
    /// <summary>
    /// Tags identifying the snapshot group.
    /// </summary>
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Host identifying the snapshot group.
    /// </summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>
    /// Paths identifying the snapshot group.
    /// </summary>
    public List<string> Paths { get; set; } = new();

    /// <summary>
    /// Array of snapshots that are kept.
    /// </summary>
    public List<Snapshot> Keep { get; set; } = new();

    /// <summary>
    /// Array of snapshots that were removed.
    /// </summary>
    public List<Snapshot> Remove { get; set; } = new();

    /// <summary>
    /// Array of keep reasons explaining why a snapshot is kept.
    /// </summary>
    public List<KeepReason> Reasons { get; set; } = new();
}