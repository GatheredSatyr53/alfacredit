using AlfaCredit.Core.Abstractions;
using AlfaCredit.Core.Common;
using AlfaCredit.Core.Domain.Applications;
using AlfaCredit.Core.Domain.Contracts;
using AlfaCredit.Core.Domain.Feed;
using AlfaCredit.Core.Domain.Processing;
using AlfaCredit.Core.Features.Applications;
using AlfaCredit.Core.Features.Feed;
using AlfaCredit.Core.Features.Processing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AlfaCredit.Core.Features.Contracts;

/// <summary>Результат запроса кода: код раскрывается в ответе только в демо-режиме.</summary>
public sealed record SigningCodeIssued(string ContractNumber, DateTimeOffset CodeExpiresAt, int AttemptsLeft, string? RevealedCode);

/// <summary>
/// Договор по заявке: просмотр, запрос кода подтверждения, подписание. Подписание ставит в очередь выдачу средств.
/// Описано в docs/completion-flow.md.
/// </summary>
public sealed class ContractService(
    ApplicationService applications,
    IUserRepository users,
    IProcessingJobRepository jobs,
    FeedService feed,
    IApplicationNotifier notifier,
    ISigningCodeGenerator codes,
    IUnitOfWork unitOfWork,
    TimeProvider clock,
    IOptions<ContractOptions> options,
    IOptions<ProcessingOptions> processingOptions,
    ILogger<ContractService> logger)
{
    public async Task<(CreditApplication Application, LoanContract Contract)> GetAsync(Guid applicationId, Actor actor, CancellationToken cancellationToken)
    {
        var application = await applications.GetAsync(applicationId, actor, cancellationToken);
        var contract = application.CurrentContract
            ?? throw new NotFoundException("Договор по заявке ещё не сформирован: предложение банка не принято.");
        return (application, contract);
    }

    public async Task<SigningCodeIssued> RequestSigningCodeAsync(Guid applicationId, Actor actor, CancellationToken cancellationToken)
    {
        var application = await GetForClientAsync(applicationId, actor, "Запросить код подписания может только клиент.", cancellationToken);
        var now = clock.GetUtcNow();
        var contract = application.RequireSignableContract(now);

        var code = codes.Next();
        contract.RequestCode(code, now, options.Value.CodeTtl);

        // Доставка кода: лента кабинета вместо SMS, которые запрещены кейсом
        feed.NotifyCompany(
            application,
            FeedItemType.SigningCode,
            $"Код подтверждения для договора {contract.Number}",
            $"Код подтверждения для подписания договора {contract.Number}: {code}. Действует {options.Value.CodeTtlMinutes} минут. Никому не сообщайте код.");
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Signing code issued for contract {Contract} (application {Number}), expires at {ExpiresAt}", contract.Number, application.Number, contract.CodeExpiresAt);
        return new SigningCodeIssued(contract.Number, contract.CodeExpiresAt!.Value, contract.AttemptsLeft, options.Value.RevealCodeInResponse ? code : null);
    }

    public async Task<CreditApplication> SignAsync(Guid applicationId, string? code, Actor actor, CancellationToken cancellationToken)
    {
        var application = await GetForClientAsync(applicationId, actor, "Подписать договор может только клиент.", cancellationToken);
        var signer = await users.FindByIdAsync(actor.UserId, cancellationToken);
        var now = clock.GetUtcNow();

        LoanContract contract;
        try
        {
            contract = application.SignContract(code, actor.UserId, signer?.FullName ?? actor.UserId.ToString(), now);
        }
        catch (ValidationException)
        {
            // Неверный код: израсходованная попытка должна сохраниться, иначе перебор кодов бесконечен
            await unitOfWork.SaveChangesAsync(cancellationToken);
            throw;
        }

        jobs.Add(ProcessingJob.Create(application.Id, ProcessingStage.Disburse, now, processingOptions.Value.StageDelay, processingOptions.Value.MaxAttempts));
        feed.NotifyCompany(
            application,
            FeedItemType.ContractSigned,
            $"Договор {contract.Number} подписан",
            $"Договор подписан простой электронной подписью. Банк переводит {Money.FormatRub(contract.Amount)} на расчётный счёт {contract.DisbursementAccount}.");
        await feed.NotifyManagersAsync(
            application,
            FeedItemType.ContractSigned,
            $"Договор {contract.Number} подписан клиентом",
            $"{application.Company.Name}: {Money.FormatRub(contract.Amount)} на {contract.TermMonths} мес. под {contract.InterestRate:0.##} %. Выдача запущена.",
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await ApplicationNotifications.NotifySafeAsync(notifier, logger, application, cancellationToken);

        logger.LogInformation("Contract {Contract} signed for application {Number}, disbursement scheduled", contract.Number, application.Number);
        return application;
    }

    private async Task<CreditApplication> GetForClientAsync(Guid applicationId, Actor actor, string forbiddenMessage, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        if (!actor.IsClient)
        {
            throw new ForbiddenException(forbiddenMessage);
        }

        return await applications.GetAsync(applicationId, actor, cancellationToken);
    }
}
