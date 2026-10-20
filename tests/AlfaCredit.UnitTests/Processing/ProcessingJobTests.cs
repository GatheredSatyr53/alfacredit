using AlfaCredit.Core.Domain.Processing;
using AlfaCredit.UnitTests.Applications;
using Shouldly;

namespace AlfaCredit.UnitTests.Processing;

public sealed class ProcessingJobTests
{
    [Fact]
    public void Failed_job_is_retried_until_attempts_are_exhausted()
    {
        var job = ProcessingJob.Create(Guid.NewGuid(), ProcessingStage.Enrich, TestData.Now, TimeSpan.Zero, maxAttempts: 2);

        job.MarkRunning(TestData.Now);
        job.MarkFailed("boom", TestData.Now, TimeSpan.FromSeconds(5)).ShouldBeTrue();
        job.Status.ShouldBe(ProcessingJobStatus.Pending);
        job.NextRunAt.ShouldBe(TestData.Now.AddSeconds(5));

        job.MarkRunning(TestData.Now);
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
}
