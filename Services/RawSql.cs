using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using NordicBeesERP.Data;

namespace NordicBeesERP.Services;

/// <summary>
/// Small ADO.NET helpers for tables that are accessed only through raw SQL (the context is globally NoTracking and
/// these tables have no DbSet). Reads use @p0.. parameters; writes elsewhere use ExecuteSqlRawAsync with {0} placeholders.
/// </summary>
public static class RawSql
{
    public static async Task<List<T>> QueryAsync<T>(
        NordicBeesERPContext db, string sql, Func<DbDataReader, T> map, CancellationToken ct = default, params object?[] args)
    {
        var conn = db.Database.GetDbConnection();
        var openedByUs = conn.State != ConnectionState.Open;
        if (openedByUs) await conn.OpenAsync(ct);
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            var current = db.Database.CurrentTransaction;
            if (current is not null) cmd.Transaction = current.GetDbTransaction();
            AddParams(cmd, args);
            var list = new List<T>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) list.Add(map(reader));
            return list;
        }
        finally
        {
            if (openedByUs) await conn.CloseAsync();
        }
    }

    public static async Task<object?> ScalarAsync(NordicBeesERPContext db, string sql, CancellationToken ct = default, params object?[] args)
    {
        var conn = db.Database.GetDbConnection();
        var openedByUs = conn.State != ConnectionState.Open;
        if (openedByUs) await conn.OpenAsync(ct);
        try
        {
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            var current = db.Database.CurrentTransaction;
            if (current is not null) cmd.Transaction = current.GetDbTransaction();
            AddParams(cmd, args);
            var value = await cmd.ExecuteScalarAsync(ct);
            return value is DBNull ? null : value;
        }
        finally
        {
            if (openedByUs) await conn.CloseAsync();
        }
    }

    private static void AddParams(DbCommand cmd, object?[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = $"@p{i}";
            p.Value = args[i] ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }
    }

    public static bool IsNull(this DbDataReader r, string col) => r.IsDBNull(r.GetOrdinal(col));
    public static string? Str(this DbDataReader r, string col) => r.IsNull(col) ? null : Convert.ToString(r.GetValue(r.GetOrdinal(col)));
    public static int Int(this DbDataReader r, string col) => Convert.ToInt32(r.GetValue(r.GetOrdinal(col)));
    public static int? IntOrNull(this DbDataReader r, string col) => r.IsNull(col) ? null : r.Int(col);
    public static long Long(this DbDataReader r, string col) => Convert.ToInt64(r.GetValue(r.GetOrdinal(col)));
    public static decimal Dec(this DbDataReader r, string col) => Convert.ToDecimal(r.GetValue(r.GetOrdinal(col)));
    public static decimal? DecOrNull(this DbDataReader r, string col) => r.IsNull(col) ? null : r.Dec(col);
    public static bool Bool(this DbDataReader r, string col) => Convert.ToInt32(r.GetValue(r.GetOrdinal(col))) != 0;
    public static DateTime Utc(this DbDataReader r, string col) => DateTime.SpecifyKind(Convert.ToDateTime(r.GetValue(r.GetOrdinal(col))), DateTimeKind.Utc);
    public static DateTime? UtcOrNull(this DbDataReader r, string col) => r.IsNull(col) ? null : r.Utc(col);
    public static DateOnly Date(this DbDataReader r, string col) => DateOnly.FromDateTime(Convert.ToDateTime(r.GetValue(r.GetOrdinal(col))));
    public static DateOnly? DateOrNull(this DbDataReader r, string col) => r.IsNull(col) ? null : r.Date(col);
}
