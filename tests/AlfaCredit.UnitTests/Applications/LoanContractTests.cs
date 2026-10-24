using AlfaCredit.Core.Common;
using AlfaCredit.Core.Domain.Applications;
using AlfaCredit.Core.Domain.Contracts;
using AlfaCredit.Core.Domain.Decisions;
using AlfaCredit.Core.Domain.Products;
using Shouldly;

namespace AlfaCredit.UnitTests.Applications;

public sealed class LoanContractTests
{
    internal static readonly Guid UserId = Guid.NewGuid();
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    [Fact]
    public void Contract_is_created_from_the_accepted_offer_with_totals_and_deadline()
    {
        var (application, contract) = SigningApplication();

        contract.Status.ShouldBe(ContractStatus.AwaitingSignature);
        contract.Amount.ShouldBe(1_500_000);
        contract.TermMonths.ShouldBe(12);
        contract.InterestRate.ShouldBe(19.5m);
        contract.MonthlyPayment.ShouldBe(138_593.06m);
        contract.TotalInterest.ShouldBeInRange(160_000m, 170_000m);
        contract.TotalPayment.ShouldBe(contract.Amount + contract.TotalInterest);
        contract.TotalPayment.ShouldBe(application.CurrentOffer!.TotalPayment, "договор повторяет итоги принятого предложения");
        contract.TotalInterest.ShouldBe(application.CurrentOffer.TotalInterest);
        contract.SigningDeadline.ShouldBe(TestData.Now.AddHours(1).AddDays(5));
        contract.DisbursementAccount.Length.ShouldBe(20);
        contract.DisbursementAccount.ShouldStartWith("40702810");
        contract.AttemptsLeft.ShouldBe(5);
        application.CurrentContract.ShouldBe(contract);
        application.AvailableActions(ActorType.Client).ShouldBe(["signContract", "cancel"], ignoreOrder: true);
        application.AvailableActions(ActorType.Manager).ShouldBeEmpty();
    }

    [Fact]
    public void Signing_requires_a_fresh_code_and_counts_wrong_attempts()
    {
        var (application, contract) = SigningApplication();
        var now = TestData.Now.AddHours(2);

        Should.Throw<ConflictException>(() => application.SignContract("123456", UserId, "Иванова", now), "код ещё не запрашивали");

        contract.RequestCode("483921", now, Ttl);
        contract.CodeExpiresAt.ShouldBe(now.Add(Ttl));
        var wrong = Should.Throw<ValidationException>(() => application.SignContract("000000", UserId, "Иванова", now.AddSeconds(5)));
        wrong.Errors["code"][0].ShouldContain("Осталось попыток: 4");
        contract.AttemptsLeft.ShouldBe(4);
        application.Status.ShouldBe(ApplicationStatus.ContractSigning);

        application.SignContract(" 483921 ", UserId, "Иванова Мария Сергеевна", now.AddSeconds(10));

        contract.Status.ShouldBe(ContractStatus.Signed);
        contract.SignedAt.ShouldBe(now.AddSeconds(10));
        contract.SignedByUserId.ShouldBe(UserId);
        contract.SignedByName.ShouldBe("Иванова Мария Сергеевна");
        contract.SignatureMethod.ShouldBe(LoanContract.SimpleSignatureMethod);
        contract.ConfirmationCode.ShouldBeNull();
        application.Status.ShouldBe(ApplicationStatus.Signed);
        application.AvailableActions(ActorType.Client).ShouldBeEmpty();
        application.History[^1].Comment!.ShouldContain("подписан простой электронной подписью");
        Should.Throw<ConflictException>(() => application.SignContract("483921", UserId, "Иванова", now.AddSeconds(20)), "повторное подписание");
        Should.Throw<ConflictException>(() => contract.RequestCode("111111", now.AddSeconds(20), Ttl), "код после подписания");
    }

    [Fact]
    public void Expired_code_and_exhausted_attempts_require_a_new_code()
    {
        var (application, contract) = SigningApplication();
        var now = TestData.Now.AddHours(2);

        contract.RequestCode("111111", now, Ttl);
        Should.Throw<ConflictException>(() => application.SignContract("111111", UserId, "x", now.AddMinutes(11)), "код просрочен");

        contract.RequestCode("222222", now.AddMinutes(11), Ttl);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            Should.Throw<ValidationException>(() => application.SignContract("999999", UserId, "x", now.AddMinutes(12)));
        }

        contract.AttemptsLeft.ShouldBe(0);
        Should.Throw<ConflictException>(() => application.SignContract("222222", UserId, "x", now.AddMinutes(12)), "попытки исчерпаны даже для верного кода");

        contract.RequestCode("333333", now.AddMinutes(13), Ttl);
        contract.AttemptsLeft.ShouldBe(5);
        application.SignContract("333333", UserId, "x", now.AddMinutes(14));
        application.Status.ShouldBe(ApplicationStatus.Signed);
    }

    [Fact]
    public void Client_can_withdraw_before_signing_and_deadline_cancels_the_application()
    {
        var (application, contract) = SigningApplication();

        application.Cancel(UserId, null, TestData.Now.AddHours(2));

        application.Status.ShouldBe(ApplicationStatus.Cancelled);
        contract.Status.ShouldBe(ContractStatus.Cancelled);
        application.History[^1].Comment.ShouldBe("Клиент отказался от подписания договора.");

        var (expiring, expiringContract) = SigningApplication();
        expiring.ExpireContractIfNeeded(TestData.Now.AddDays(3)).ShouldBeFalse();
        expiring.ExpireContractIfNeeded(TestData.Now.AddDays(6)).ShouldBeTrue();
        expiring.Status.ShouldBe(ApplicationStatus.Cancelled);
        expiringContract.Status.ShouldBe(ContractStatus.Expired);
        expiring.History[^1].ActorType.ShouldBe(ActorType.System);
        Should.Throw<ConflictException>(() => expiring.RequireSignableContract(TestData.Now.AddDays(6)));
    }

    [Fact]
    public void Disbursement_completes_the_application_only_after_signing()
    {
        var (application, contract) = SigningApplication();
        var now = TestData.Now.AddHours(2);

        var early = Disbursement.Completed(application.Id, contract, "PO-1", now, now);
        Should.Throw<ConflictException>(() => application.RecordDisbursement(early, now));

        contract.RequestCode("483921", now, Ttl);
        application.SignContract("483921", UserId, "x", now);

        var failed = Disbursement.Failed(application.Id, contract, "outage", now.AddSeconds(1));
        application.RecordDisbursement(failed, now.AddSeconds(1));
        application.Status.ShouldBe(ApplicationStatus.Signed, "неудачная попытка не меняет статус");
        failed.Error.ShouldBe("outage");
        failed.PaymentOrderNumber.ShouldBeNull();

        var done = Disbursement.Completed(application.Id, contract, "PO-20261023-0001", now.AddSeconds(2), now.AddSeconds(3));
        application.RecordDisbursement(done, now.AddSeconds(3));

        application.Status.ShouldBe(ApplicationStatus.Disbursed);
        application.IsTerminal.ShouldBeTrue();
        application.CurrentDisbursement.ShouldBe(done);
        done.Account.ShouldBe(contract.DisbursementAccount);
        done.Amount.ShouldBe(contract.Amount);
        application.History[^1].Comment!.ShouldContain("PO-20261023-0001");
        application.AvailableActions(ActorType.Client).ShouldBeEmpty();
    }

    [Fact]
    public void Settlement_account_is_deterministic_and_depends_on_legal_form()
    {
        var ooo = BankAccounts.SettlementAccountFor("7701234567", Core.Domain.Companies.LegalForm.Ooo);
        var ip = BankAccounts.SettlementAccountFor("503212345678", Core.Domain.Companies.LegalForm.Ip);

        ooo.ShouldBe(BankAccounts.SettlementAccountFor("7701234567", Core.Domain.Companies.LegalForm.Ooo));
        ooo.Length.ShouldBe(20);
        ooo.ShouldStartWith("40702810");
        ip.ShouldStartWith("40802810");
        ooo.ShouldAllBe(c => char.IsDigit(c));
        ooo.ShouldNotBe(BankAccounts.SettlementAccountFor("7812345678", Core.Domain.Companies.LegalForm.Ooo));
    }

    /// <summary>Заявка, доведённая до принятия предложения, с прикреплённым договором.</summary>
    internal static (CreditApplication Application, LoanContract Contract) SigningApplication(decimal amount = 1_500_000, int termMonths = 12)
    {
        var application = CreditApplication.Create(TestData.Company(memberUserId: UserId), TestData.Product(), UserId, amount, termMonths, LoanPurpose.WorkingCapital, null, TestData.Now);
        application.BeginValidation(TestData.Now);
        application.BeginEnrichment(TestData.Now);
        application.BeginScoring(TestData.Now);
        application.BeginDeciding(TestData.Now);
        var payment = LoanMath.MonthlyPayment(amount, 19.5m, termMonths);
        var decision = Decision.Create(application.Id, DecisionType.Approved, DecisionActor.Auto, null, null, amount, termMonths, 19.5m, payment, null, [], TestData.Now);
        application.ApplyDecision(decision, TimeSpan.FromDays(7), TestData.Now);

        var acceptedAt = TestData.Now.AddHours(1);
        var offer = application.AcceptOffer(UserId, acceptedAt);
        var contract = LoanContract.Create(
            application.Id,
            offer,
            "AlfaCredit (демонстрационный банк)",
            application.Company.Name,
            application.Company.Inn,
            BankAccounts.SettlementAccountFor(application.Company.Inn, application.Company.LegalForm),
            acceptedAt,
            TimeSpan.FromDays(5),
            codeMaxAttempts: 5);
        application.AttachContract(contract);
        return (application, contract);
    }
}
