using ApiPlayground.Application.Outbound;
using ApiPlayground.Api.Data;
using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Text.RegularExpressions;

namespace ApiPlayground.Infrastructure.Outbound;

public sealed class PostgresTracerPersistenceSmokeCheck(PlatformDbContext dbContext) : ITracerPersistenceSmokeCheck
{
    public async Task<bool> VerifyRoundTripAsync(CancellationToken cancellationToken = default)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
            await dbContext.Database.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            var tableName = $"apg_tracer_smoke_{Guid.NewGuid():N}";
            if (!Regex.IsMatch(tableName, @"\Aapg_tracer_smoke_[0-9a-f]{32}\z", RegexOptions.CultureInvariant))
                throw new InvalidOperationException("Refusing a smoke-check table name outside the exact generated pattern.");
            await using var create = connection.CreateCommand();
            create.Transaction = transaction;
            create.CommandText = $"CREATE TABLE public.\"{tableName}\" (marker text NOT NULL)";
            await create.ExecuteNonQueryAsync(cancellationToken);

            const string marker = "tracer-ready";
            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = $"INSERT INTO public.\"{tableName}\" (marker) VALUES (@marker)";
            var insertParameter = insert.CreateParameter();
            insertParameter.ParameterName = "marker";
            insertParameter.Value = marker;
            insert.Parameters.Add(insertParameter);
            await insert.ExecuteNonQueryAsync(cancellationToken);

            await using var read = connection.CreateCommand();
            read.Transaction = transaction;
            read.CommandText = $"SELECT marker FROM public.\"{tableName}\" LIMIT 1";
            var observed = await read.ExecuteScalarAsync(cancellationToken);
            await transaction.RollbackAsync(cancellationToken);
            return string.Equals(observed as string, marker, StringComparison.Ordinal);
        }
        finally
        {
            if (openedHere)
                await dbContext.Database.CloseConnectionAsync();
        }
    }
}
