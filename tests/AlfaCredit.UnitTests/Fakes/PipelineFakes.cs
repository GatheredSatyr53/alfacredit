using AlfaCredit.Core.Abstractions;
using AlfaCredit.Core.Common;
using AlfaCredit.Core.Domain.Checks;
using AlfaCredit.Core.Domain.Companies;
using AlfaCredit.Core.Domain.Feed;
using AlfaCredit.Core.Domain.Processing;
using AlfaCredit.Core.Domain.Users;

namespace AlfaCredit.UnitTests.Fakes;

internal sealed class InMemoryJobRepository : IProcessingJobRepository
{
    public List<ProcessingJob> Items { get; } = [];

    public void Add(ProcessingJob job) => Items.Add(job);

    public Task<ProcessingJob?> ClaimNextAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var job = Items.Where(j => j.Status == ProcessingJobStatus.Pending && j.NextRunAt <= now).OrderBy(j => j.NextRunAt).FirstOrDefault();
        job?.MarkRunning(now);
        return Task.FromResult(job);
    }

    public Task<ProcessingJob?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(j => j.Id == id));
}

internal sealed class InMemoryFeedRepository : IFeedRepository
{
    public List<FeedItem> Items { get; } = [];

    public void Add(FeedItem item) => Items.Add(item);

    public Task<PagedResult<FeedItem>> ListAsync(Guid userId, bool unreadOnly, int page, int pageSize, CancellationToken cancellationToken)
    {
        var all = Items.Where(f => f.UserId == userId && (!unreadOnly || !f.IsRead)).OrderByDescending(f => f.CreatedAt).ToArray();
        return Task.FromResult(new PagedResult<FeedItem>(all.Skip((page - 1) * pageSize).Take(pageSize).ToArray(), page, pageSize, all.Length));
    }

    public Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Count(f => f.UserId == userId && !f.IsRead));

    public Task<FeedItem?> FindAsync(Guid id, Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(f => f.Id == id && f.UserId == userId));

    public Task<IReadOnlyList<FeedItem>> ListUnreadAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<FeedItem>>(Items.Where(f => f.UserId == userId && !f.IsRead).ToArray());
}

internal sealed class RecordingNotifier : IApplicationNotifier
{
    public List<ApplicationChangedNotification> Notifications { get; } = [];

    public Task ApplicationChangedAsync(ApplicationChangedNotification notification, CancellationToken cancellationToken)
    {
        Notifications.Add(notification);
        return Task.CompletedTask;
    }
}

/// <summary>Симулятор для тестов: отдаёт заданные результаты, умеет падать заданное число раз.</summary>
internal sealed class FakeSources : IEgrulClient, IFnsClient, ICreditBureauClient, IFsspClient
{
    public EgrulResult Egrul { get; set; } = new(EgrulStatus.Active, false, false, new DateOnly(2019, 3, 12), "47.11");

    public FnsResult Fns { get; set; } = new(0, false, 47_300_000);

    public CreditBureauResult Bureau { get; set; } = new(60, 1, 3, 1_200_000, 150_000, 0, 0, false);

    public FsspResult Fssp { get; set; } = new(0, 0);

    public Dictionary<CheckSource, int> FailuresLeft { get; } = [];

    public Dictionary<CheckSource, int> Calls { get; } = [];

    Task<EgrulResult> IEgrulClient.GetAsync(Company company, CancellationToken cancellationToken) => Serve(CheckSource.Egrul, Egrul);

    Task<FnsResult> IFnsClient.GetAsync(Company company, CancellationToken cancellationToken) => Serve(CheckSource.Fns, Fns);

    Task<CreditBureauResult> ICreditBureauClient.GetAsync(Company company, CancellationToken cancellationToken) => Serve(CheckSource.CreditBureau, Bureau);

    Task<FsspResult> IFsspClient.GetAsync(Company company, CancellationToken cancellationToken) => Serve(CheckSource.Fssp, Fssp);

    private Task<T> Serve<T>(CheckSource source, T value)
    {
        Calls[source] = Calls.GetValueOrDefault(source) + 1;
        if (FailuresLeft.GetValueOrDefault(source) > 0)
        {
            FailuresLeft[source]--;
            throw new ExternalSourceUnavailableException(source, "simulated outage");
        }

        return Task.FromResult(value);
    }
}

internal sealed class InMemoryUsersByRole(params IEnumerable<User> users) : IUserRepository
{
    private readonly List<User> _users = [.. users];

    public Task<User?> FindByLoginAsync(string login, CancellationToken cancellationToken) =>
        Task.FromResult(_users.FirstOrDefault(u => u.Login == login));

    public Task<User?> FindByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_users.FirstOrDefault(u => u.Id == id));

    public Task<IReadOnlyList<User>> ListActiveByRoleAsync(UserRole role, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<User>>(_users.Where(u => u.Role == role && u.IsActive).ToArray());
}
