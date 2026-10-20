using AlfaCredit.Core.Common;
using AlfaCredit.Core.Domain.Applications;
using Shouldly;

namespace AlfaCredit.UnitTests.Applications;

public sealed class ApplicationWorkflowTests
{
    [Theory]
    [InlineData(ApplicationStatus.Submitted, ApplicationStatus.Validating, ActorType.System, true)]
    [InlineData(ApplicationStatus.Submitted, ApplicationStatus.Validating, ActorType.Client, false)]
    [InlineData(ApplicationStatus.Submitted, ApplicationStatus.Cancelled, ActorType.Client, true)]
    [InlineData(ApplicationStatus.Submitted, ApplicationStatus.Approved, ActorType.System, false)]
    [InlineData(ApplicationStatus.Deciding, ApplicationStatus.Rejected, ActorType.System, true)]
    [InlineData(ApplicationStatus.Deciding, ApplicationStatus.Rejected, ActorType.Manager, false)]
    [InlineData(ApplicationStatus.ManualReview, ApplicationStatus.Approved, ActorType.Manager, true)]
    [InlineData(ApplicationStatus.ManualReview, ApplicationStatus.Approved, ActorType.System, false)]
    [InlineData(ApplicationStatus.Approved, ApplicationStatus.ContractSigning, ActorType.Client, true)]
    [InlineData(ApplicationStatus.Approved, ApplicationStatus.Cancelled, ActorType.Client, false)]
    [InlineData(ApplicationStatus.Rejected, ApplicationStatus.Submitted, ActorType.Client, false)]
    [InlineData(ApplicationStatus.Disbursed, ApplicationStatus.Cancelled, ActorType.Client, false)]
    public void CanTransition_follows_the_transition_table(ApplicationStatus from, ApplicationStatus to, ActorType actor, bool expected)
    {
        ApplicationWorkflow.CanTransition(from, to, actor).ShouldBe(expected);
    }

    [Fact]
    public void Terminal_statuses_have_no_outgoing_transitions()
    {
        foreach (var info in ApplicationStatusCatalog.All.Where(i => i.IsTerminal))
        {
            foreach (var actor in Enum.GetValues<ActorType>())
            {
                ApplicationWorkflow.NextStatuses(info.Code, actor).ShouldBeEmpty($"{info.Code} / {actor}");
            }
        }
    }

    [Fact]
    public void Every_status_is_described_in_the_catalog()
    {
        foreach (var status in Enum.GetValues<ApplicationStatus>())
        {
            var info = ApplicationStatusCatalog.Get(status);
            info.Title.ShouldNotBeNullOrWhiteSpace();
            info.ClientDescription.ShouldNotBeNullOrWhiteSpace();
            info.ClientNextAction.ShouldNotBeNullOrWhiteSpace();
            info.ProgressStep.ShouldBeInRange(0, ApplicationStatusCatalog.TotalProgressSteps);
        }
    }

    [Fact]
    public void EnsureAllowed_throws_with_readable_message_for_forbidden_transition()
    {
        var exception = Should.Throw<InvalidStatusTransitionException>(() =>
            ApplicationWorkflow.EnsureAllowed(ApplicationStatus.Rejected, ApplicationStatus.Cancelled, ActorType.Client));

        exception.From.ShouldBe(ApplicationStatus.Rejected);
        exception.To.ShouldBe(ApplicationStatus.Cancelled);
        exception.Message.ShouldContain("Отказ");
        exception.Message.ShouldContain("Отменена");
    }
}
