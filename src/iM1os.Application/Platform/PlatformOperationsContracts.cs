namespace iM1os.Application.Platform;

public sealed record PlatformOperationsPage(
    DateTimeOffset CheckedAtUtc,
    string DatabaseName,
    int ActiveDatabaseSessionCount,
    int IndexBuildCount,
    int RunningImportCount,
    int RecentFailedImportCount,
    IReadOnlyCollection<string> Warnings,
    IReadOnlyCollection<PlatformDatabaseSessionRow> ActiveDatabaseSessions,
    IReadOnlyCollection<PlatformIndexBuildRow> IndexBuilds,
    IReadOnlyCollection<PlatformIndexUsageRow> LargestIndexes,
    IReadOnlyCollection<PlatformTableSizeRow> LargestTables,
    IReadOnlyCollection<PlatformImportRunStatusRow> RecentImportRuns);

public sealed record PlatformDatabaseSessionRow(
    int ProcessId,
    string UserName,
    string State,
    string? WaitEventType,
    string? WaitEvent,
    string Age,
    string Query);

public sealed record PlatformIndexBuildRow(
    int ProcessId,
    string TableName,
    string? IndexName,
    string Phase,
    long BlocksDone,
    long BlocksTotal,
    long TuplesDone,
    long TuplesTotal);

public sealed record PlatformIndexUsageRow(
    string TableName,
    string IndexName,
    string Size,
    long IndexScans,
    long TuplesRead,
    long TuplesFetched);

public sealed record PlatformTableSizeRow(
    string TableName,
    long EstimatedRows,
    string TotalSize,
    string TableSize,
    string IndexSize);

public sealed record PlatformImportRunStatusRow(
    string Scope,
    string ImportType,
    string Status,
    DateTimeOffset RequestedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string? Source,
    int ProgressProcessed,
    int? ProgressTotal,
    string? Message);
