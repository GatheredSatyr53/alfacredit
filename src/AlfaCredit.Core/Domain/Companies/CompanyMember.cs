namespace AlfaCredit.Core.Domain.Companies;

/// <summary>Связь пользователя с компанией: кто может подавать заявки от её имени.</summary>
public sealed class CompanyMember
{
    private CompanyMember()
    {
    }

    public Guid Id { get; private set; }

    public Guid CompanyId { get; private set; }

    public Guid UserId { get; private set; }

    public CompanyMemberRole Role { get; private set; }

    public DateTimeOffset JoinedAt { get; private set; }

    internal static CompanyMember Create(Guid companyId, Guid userId, CompanyMemberRole role, DateTimeOffset joinedAt) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            CompanyId = companyId,
            UserId = userId,
            Role = role,
            JoinedAt = joinedAt,
        };
}
