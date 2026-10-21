using ApiPlayground.Application.Outbound;
using ApiPlayground.Api.Data;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace ApiPlayground.Infrastructure.Outbound;

public sealed class PostgresTracerPersistenceSmokeCheck(PlatformDbContext dbContext) : ITracerPersistenceSmokeCheck
{
    public async Task<bool> VerifyRoundTripAsync(CancellationToken cancellationToken = default)
    {
        var connection = dbContext.Database.GetDbConnection();
        var openedHere = connection.State != ConnectionState.Open;
        if (openedHere)
            await dbContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using var create = connection.CreateCommand();
            create.Transaction = transaction;
            create.CommandText = "CREATE TEMP TABLE IF NOT EXISTS apiplayground_tracer_smoke (marker text NOT NULL) ON COMMIT DROP";
            await create.ExecuteNonQueryAsync(cancellationToken);

            const string marker = "tracer-ready";
            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO apiplayground_tracer_smoke (marker) VALUES (@marker)";
            var insertParameter = insert.CreateParameter();
            insertParameter.ParameterName = "marker";
            insertParameter.Value = marker;
            insert.Parameters.Add(insertParameter);
            await insert.ExecuteNonQueryAsync(cancellationToken);

            await using var read = connection.CreateCommand();
            read.Transaction = transaction;
            read.CommandText = "SELECT marker FROM apiplayground_tracer_smoke LIMIT 1";
            var observed = await read.ExecuteScalarAsync(cancellationToken);
            await using var delete = connection.CreateCommand();
            delete.Transaction = transaction;
            delete.CommandText = "DELETE FROM apiplayground_tracer_smoke WHERE marker = @marker";
            var deleteParameter = delete.CreateParameter();
            deleteParameter.ParameterName = "marker";
            deleteParameter.Value = marker;
            delete.Parameters.Add(deleteParameter);
            var removed = await delete.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return string.Equals(observed as string, marker, StringComparison.Ordinal) && removed == 1;
        }
        finally
        {
            if (openedHere)
                await dbContext.Database.CloseConnectionAsync();
        }
    }
}
