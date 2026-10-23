using AlfaCredit.Core.Abstractions;
using AlfaCredit.Core.Domain.Applications;
using AlfaCredit.Core.Domain.Checks;
using AlfaCredit.Core.Domain.Companies;
using AlfaCredit.Core.Domain.Contracts;
using AlfaCredit.Core.Domain.Decisions;
using AlfaCredit.Core.Domain.Feed;
using AlfaCredit.Core.Domain.Processing;
using AlfaCredit.Core.Domain.Scoring;
using AlfaCredit.Core.Domain.Products;
using AlfaCredit.Core.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace AlfaCredit.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IUnitOfWork
{
    /// <summary>Последовательность для человекочитаемых номеров заявок AC-000001.</summary>
    public const string ApplicationNumberSequence = "application_number_seq";

    /// <summary>Последовательность для номеров договоров KD-000001.</summary>
    public const string ContractNumberSequence = "contract_number_seq";

    public DbSet<User> Users => Set<User>();

    public DbSet<Company> Companies => Set<Company>();

    public DbSet<CompanyMember> CompanyMembers => Set<CompanyMember>();

    public DbSet<CreditProduct> CreditProducts => Set<CreditProduct>();

    public DbSet<CreditApplication> CreditApplications => Set<CreditApplication>();

    public DbSet<ApplicationStatusHistory> ApplicationStatusHistory => Set<ApplicationStatusHistory>();

    public DbSet<ProcessingJob> ProcessingJobs => Set<ProcessingJob>();

    public DbSet<ExternalCheck> ExternalChecks => Set<ExternalCheck>();

    public DbSet<ScoringResult> ScoringResults => Set<ScoringResult>();

    public DbSet<Decision> Decisions => Set<Decision>();

    public DbSet<Offer> Offers => Set<Offer>();

    public DbSet<LoanContract> LoanContracts => Set<LoanContract>();

    public DbSet<Disbursement> Disbursements => Set<Disbursement>();

    public DbSet<FeedItem> FeedItems => Set<FeedItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasSequence<long>(ApplicationNumberSequence).StartsAt(1).IncrementsBy(1);
        modelBuilder.HasSequence<long>(ContractNumberSequence).StartsAt(1).IncrementsBy(1);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
