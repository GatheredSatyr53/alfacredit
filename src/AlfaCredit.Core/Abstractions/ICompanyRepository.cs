using AlfaCredit.Core.Domain.Companies;

namespace AlfaCredit.Core.Abstractions;

public interface ICompanyRepository
{
    /// <summary>Компания вместе с участниками.</summary>
    Task<Company?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Company>> ListForUserAsync(Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Company>> ListAllAsync(CancellationToken cancellationToken);

    Task<bool> IsMemberAsync(Guid companyId, Guid userId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Guid>> GetMemberCompanyIdsAsync(Guid userId, CancellationToken cancellationToken);
}
