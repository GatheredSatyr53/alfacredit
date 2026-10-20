using System.Data;
using AlfaCredit.Core.Abstractions;
using AlfaCredit.Core.Domain.Processing;
using Microsoft.EntityFrameworkCore;

namespace AlfaCredit.Infrastructure.Persistence.Repositories;

internal sealed class ProcessingJobRepository(AppDbContext db) : IProcessingJobRepository
{
    // Один UPDATE с SKIP LOCKED: несколько воркеров не возьмут одно задание дважды
    private const string ClaimSql = """
        UPDATE processing_jobs
        SET status = 'Running', attempts = attempts + 1, updated_at = @now
        WHERE id = (
            SELECT id FROM processing_jobs
            WHERE status = 'Pending' AND next_run_at <= @now
            ORDER BY next_run_at
            LIMIT 1
            FOR UPDATE SKIP LOCKED)
        RETURNING id
        """;

    public void Add(ProcessingJob job) => db.ProcessingJobs.Add(job);

    public async Task<ProcessingJob?> ClaimNextAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var connection = db.Database.GetDbConnection();
        var wasClosed = connection.State != ConnectionState.Open;
        if (wasClosed)
        {
            await db.Database.OpenConnectionAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = ClaimSql;
            var parameter = command.CreateParameter();
            parameter.ParameterName = "now";
            parameter.Value = now;
            command.Parameters.Add(parameter);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            if (result is not Guid id)
            {
                return null;
            }

            return await db.ProcessingJobs.FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
        }
        finally
        {
            if (wasClosed)
            {
                await db.Database.CloseConnectionAsync();
            }
        }
    }

    public Task<ProcessingJob?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.ProcessingJobs.FirstOrDefaultAsync(j => j.Id == id, cancellationToken);
}
