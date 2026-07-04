using System.Data;
using System.Data.Common;
using iM1os.Application.Platform;
using iM1os.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace iM1os.Infrastructure.Services;

public sealed class PlatformOperationsService(ApplicationDbContext dbContext) : IPlatformOperationsService
{
    public async Task<PlatformOperationsPage> GetOperationsAsync(CancellationToken cancellationToken)
    {
        var checkedAtUtc = DateTimeOffset.UtcNow;
        var warnings = new List<string>();

        var databaseName = await FirstScalarAsync("select current_database();", "unknown", warnings, cancellationToken);
        var indexBuilds = await TryQueryAsync(
            """
            select pid,
                   relid::regclass::text as table_name,
                   index_relid::regclass::text as index_name,
                   phase,
                   blocks_done,
                   blocks_total,
                   tuples_done,
                   tuples_total
            from pg_stat_progress_create_index
            order by pid;
            """,
            reader => new PlatformIndexBuildRow(
                GetInt32(reader, "pid"),
                GetString(reader, "table_name"),
                GetNullableString(reader, "index_name"),
                GetString(reader, "phase"),
                GetInt64(reader, "blocks_done"),
                GetInt64(reader, "blocks_total"),
                GetInt64(reader, "tuples_done"),
                GetInt64(reader, "tuples_total")),
            warnings,
            cancellationToken);

        var activeSessions = await TryQueryAsync(
            """
            select pid,
                   usename,
                   state,
                   wait_event_type,
                   wait_event,
                   now() - query_start as age,
                   left(query, 300) as query
            from pg_stat_activity
            where datname = current_database()
              and state <> 'idle'
              and query not ilike '%pg_stat_activity%'
            order by query_start
            limit 50;
            """,
            reader => new PlatformDatabaseSessionRow(
                GetInt32(reader, "pid"),
                GetString(reader, "usename"),
                GetString(reader, "state"),
                GetNullableString(reader, "wait_event_type"),
                GetNullableString(reader, "wait_event"),
                GetStringValue(reader, "age"),
                GetString(reader, "query").Trim()),
            warnings,
            cancellationToken);

        var largestIndexes = await TryQueryAsync(
            """
            select relname as table_name,
                   indexrelname as index_name,
                   pg_size_pretty(pg_relation_size(indexrelid)) as index_size,
                   idx_scan,
                   idx_tup_read,
                   idx_tup_fetch
            from pg_stat_user_indexes
            where schemaname = 'platform'
            order by pg_relation_size(indexrelid) desc
            limit 30;
            """,
            reader => new PlatformIndexUsageRow(
                GetString(reader, "table_name"),
                GetString(reader, "index_name"),
                GetString(reader, "index_size"),
                GetInt64(reader, "idx_scan"),
                GetInt64(reader, "idx_tup_read"),
                GetInt64(reader, "idx_tup_fetch")),
            warnings,
            cancellationToken);

        var largestTables = await TryQueryAsync(
            """
            select c.relname as table_name,
                   greatest(c.reltuples::bigint, 0) as estimated_rows,
                   pg_size_pretty(pg_total_relation_size(c.oid)) as total_size,
                   pg_size_pretty(pg_relation_size(c.oid)) as table_size,
                   pg_size_pretty(pg_indexes_size(c.oid)) as index_size
            from pg_class c
            join pg_namespace n on n.oid = c.relnamespace
            where n.nspname = 'platform'
              and c.relkind = 'r'
            order by pg_total_relation_size(c.oid) desc
            limit 30;
            """,
            reader => new PlatformTableSizeRow(
                GetString(reader, "table_name"),
                GetInt64(reader, "estimated_rows"),
                GetString(reader, "total_size"),
                GetString(reader, "table_size"),
                GetString(reader, "index_size")),
            warnings,
            cancellationToken);

        var globalRuns = await dbContext.SupplierConnectorImportRuns
            .AsNoTracking()
            .OrderByDescending(x => x.RequestedAtUtc)
            .Take(30)
            .Select(x => new PlatformImportRunStatusRow(
                "Platform",
                x.ImportType,
                x.Status,
                x.RequestedAtUtc,
                x.StartedAtUtc,
                x.CompletedAtUtc,
                x.Source,
                x.ProgressProcessed,
                x.ProgressTotal,
                x.Message))
            .ToListAsync(cancellationToken);
        var companyRuns = await dbContext.CompanySupplierConnectorImportRuns
            .IgnoreQueryFilters()
            .AsNoTracking()
            .OrderByDescending(x => x.RequestedAtUtc)
            .Take(30)
            .Select(x => new PlatformImportRunStatusRow(
                "Company",
                x.ImportType,
                x.Status,
                x.RequestedAtUtc,
                x.StartedAtUtc,
                x.CompletedAtUtc,
                x.Source,
                x.ProgressProcessed,
                x.ProgressTotal,
                x.Message))
            .ToListAsync(cancellationToken);
        var recentImportRuns = globalRuns
            .Concat(companyRuns)
            .OrderByDescending(x => x.RequestedAtUtc)
            .Take(40)
            .ToList();

        return new PlatformOperationsPage(
            checkedAtUtc,
            databaseName,
            activeSessions.Count,
            indexBuilds.Count,
            recentImportRuns.Count(x => string.Equals(x.Status, "Running", StringComparison.OrdinalIgnoreCase)),
            recentImportRuns.Count(x => string.Equals(x.Status, "Failed", StringComparison.OrdinalIgnoreCase)),
            warnings,
            activeSessions,
            indexBuilds,
            largestIndexes,
            largestTables,
            recentImportRuns);
    }

    private async Task<string> FirstScalarAsync(string sql, string fallback, List<string> warnings, CancellationToken cancellationToken)
    {
        try
        {
            var connection = dbContext.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = 5;
            var value = await command.ExecuteScalarAsync(cancellationToken);
            return value?.ToString() ?? fallback;
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException)
        {
            warnings.Add($"Database scalar check failed: {ex.Message}");
            return fallback;
        }
    }

    private async Task<List<T>> TryQueryAsync<T>(
        string sql,
        Func<DbDataReader, T> map,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        try
        {
            var connection = dbContext.Database.GetDbConnection();
            if (connection.State != ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandTimeout = 10;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var rows = new List<T>();
            while (await reader.ReadAsync(cancellationToken))
            {
                rows.Add(map(reader));
            }

            return rows;
        }
        catch (Exception ex) when (ex is DbException or InvalidOperationException)
        {
            warnings.Add($"Database diagnostic query failed: {ex.Message}");
            return [];
        }
    }

    private static int GetInt32(DbDataReader reader, string name)
    {
        var value = reader.GetValue(reader.GetOrdinal(name));
        return Convert.ToInt32(value);
    }

    private static long GetInt64(DbDataReader reader, string name)
    {
        var value = reader.GetValue(reader.GetOrdinal(name));
        return Convert.ToInt64(value);
    }

    private static string GetString(DbDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? string.Empty : reader.GetString(ordinal);
    }

    private static string? GetNullableString(DbDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static string GetStringValue(DbDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? string.Empty : reader.GetValue(ordinal).ToString() ?? string.Empty;
    }
}
