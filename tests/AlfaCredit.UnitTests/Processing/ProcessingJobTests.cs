using AlfaCredit.Core.Domain.Processing;
using AlfaCredit.UnitTests.Applications;
using Shouldly;

namespace AlfaCredit.UnitTests.Processing;

public sealed class ProcessingJobTests
{
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(2);

    [Fact]
    public void Failed_job_is_retried_until_attempts_are_exhausted()
    {
        var job = ProcessingJob.Create(Guid.NewGuid(), ProcessingStage.Enrich, TestData.Now, TimeSpan.Zero, maxAttempts: 2);

        job.MarkRunning(TestData.Now, Lease);
        job.MarkFailed("boom", TestData.Now, TimeSpan.FromSeconds(5)).ShouldBeTrue();
        job.Status.ShouldBe(ProcessingJobStatus.Pending);
        job.NextRunAt.ShouldBe(TestData.Now.AddSeconds(5));
        job.LeaseExpiresAt.ShouldBeNull();

        job.MarkRunning(TestData.Now, Lease);
        job.MarkFailed("boom again", TestData.Now, TimeSpan.FromSeconds(5)).ShouldBeFalse();
        job.Status.ShouldBe(ProcessingJobStatus.Failed);
        job.LastError.ShouldBe("boom again");
    }

    [Fact]
    public void Stage_delay_is_applied_to_next_run()
    {
        var job = ProcessingJob.Create(Guid.NewGuid(), ProcessingStage.Validate, TestData.Now, TimeSpan.FromSeconds(2));

        job.NextRunAt.ShouldBe(TestData.Now.AddSeconds(2));
        job.Status.ShouldBe(ProcessingJobStatus.Pending);
    }

    [Fact]
    public void Running_job_holds_a_lease_that_expires()
    {
        var job = ProcessingJob.Create(Guid.NewGuid(), ProcessingStage.Score, TestData.Now, TimeSpan.Zero);

        job.MarkRunning(TestData.Now, Lease);

        job.LeaseExpiresAt.ShouldBe(TestData.Now.Add(Lease));
        job.IsLeaseExpired(TestData.Now.AddMinutes(1)).ShouldBeFalse();
        job.IsLeaseExpired(TestData.Now.AddMinutes(2)).ShouldBeTrue();

        job.MarkDone(TestData.Now.AddMinutes(1));
        job.LeaseExpiresAt.ShouldBeNull();
        job.IsLeaseExpired(TestData.Now.AddMinutes(5)).ShouldBeFalse();
    }

    [Fact]
    public void Interrupted_job_is_requeued_without_losing_an_attempt()
    {
        var job = ProcessingJob.Create(Guid.NewGuid(), ProcessingStage.Enrich, TestData.Now, TimeSpan.Zero, maxAttempts: 3);
        job.MarkRunning(TestData.Now, Lease);
        job.Attempts.ShouldBe(1);

        job.Requeue(TestData.Now.AddSeconds(30));

        job.Status.ShouldBe(ProcessingJobStatus.Pending);
        job.Attempts.ShouldBe(0);
        job.NextRunAt.ShouldBe(TestData.Now.AddSeconds(30));
        job.LeaseExpiresAt.ShouldBeNull();
        job.LastError.ShouldBe(ProcessingJob.InterruptedError);
    }
}
