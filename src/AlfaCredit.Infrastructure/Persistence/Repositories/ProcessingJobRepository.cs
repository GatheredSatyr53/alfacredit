using System.Data;
using AlfaCredit.Core.Abstractions;
using AlfaCredit.Core.Domain.Processing;
using Microsoft.EntityFrameworkCore;

namespace AlfaCredit.Infrastructure.Persistence.Repositories;

internal sealed class ProcessingJobRepository(AppDbContext db) : IProcessingJobRepository
{
    // Один UPDATE с SKIP LOCKED: несколько воркеров не возьмут одно задание дважды.
    // Берутся готовые задания и те, у которых истекла аренда: их воркер погиб, не отметив результат.
    private const string ClaimSql = """
        UPDATE processing_jobs
        SET status = 'Running', attempts = attempts + 1, lease_expires_at = @lease, updated_at = @now
        WHERE id = (
            SELECT id FROM processing_jobs
            WHERE (status = 'Pending' AND next_run_at <= @now)
               OR (status = 'Running' AND lease_expires_at <= @now)
            ORDER BY next_run_at
            LIMIT 1
            FOR UPDATE SKIP LOCKED)
        RETURNING id
        """;

    public void Add(ProcessingJob job) => db.ProcessingJobs.Add(job);

    public async Task<ProcessingJob?> ClaimNextAsync(DateTimeOffset now, TimeSpan lease, CancellationToken cancellationToken)
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
            AddParameter(command, "now", now);
            AddParameter(command, "lease", now + (lease < TimeSpan.Zero ? TimeSpan.Zero : lease));

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

    public Task<int> RequeueInterruptedAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        db.Database.ExecuteSqlAsync(
            $"""
            UPDATE processing_jobs
            SET status = 'Pending', attempts = GREATEST(attempts - 1, 0), next_run_at = {now}, lease_expires_at = NULL,
                last_error = {ProcessingJob.InterruptedError}, updated_at = {now}
            WHERE status = 'Running'
            """,
            cancellationToken);

    public Task<ProcessingJob?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.ProcessingJobs.FirstOrDefaultAsync(j => j.Id == id, cancellationToken);

    private static void AddParameter(System.Data.Common.DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
