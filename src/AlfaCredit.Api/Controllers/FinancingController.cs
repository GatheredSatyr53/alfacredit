using AlfaCredit.Api.Auth;
using AlfaCredit.Api.Contracts.Financing;
using AlfaCredit.Api.Mapping;
using AlfaCredit.Core.Features.Financing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AlfaCredit.Api.Controllers;

/// <summary>Предварительный подбор финансирования: «нужны деньги к дате». Заявка не создаётся, расчёт не сохраняется.</summary>
[ApiController]
[Route("api/financing")]
[Produces("application/json")]
[Authorize]
public sealed class FinancingController(FinancingPreviewService previews) : ControllerBase
{
    /// <summary>Подобрать варианты финансирования по цели, сумме, дате и необязательному сроку.</summary>
    /// <remarks>
    /// Проверяет все активные продукты по тем же правилам, что подача заявки (цель, сумма, срок, возраст бизнеса),
    /// считает платёж, переплату, кредитную нагрузку и ориентир получения денег, объясняет ограничения.
    /// Если срок не указан, он подбирается для каждого продукта. Вариант из ответа отправляется как заявка телом options[].applyWith.
    /// Клиент может считать только для своих компаний, сотрудник банка для любой. Контракт: docs/financing-preview.md.
    /// </remarks>
    /// <response code="200">Расчёт выполнен; при отсутствии подходящих продуктов options пустой, причины в unsuitable.</response>
    /// <response code="400">Ошибка валидации: errors.amount, errors.purpose, errors.needByDate, errors.termMonths, errors.companyId.</response>
    /// <response code="403">Пользователь не представитель компании.</response>
    [HttpPost("preview")]
    [ProducesResponseType<FinancingPreviewDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<FinancingPreviewDto>> Preview(FinancingPreviewRequest request, CancellationToken cancellationToken)
    {
        var query = new FinancingPreviewQuery(
            request.CompanyId!.Value,
            request.Amount,
            request.Purpose!.Value,
            request.NeedByDate!.Value,
            request.TermMonths);

        var preview = await previews.PreviewAsync(query, User.ToActor(), cancellationToken);
        return preview.ToDto();
    }
}
