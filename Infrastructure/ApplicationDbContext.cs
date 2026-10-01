using Application.Common.Interfaces;
using Domain.Common;
using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace Infrastructure
{
    public class ApplicationDbContext : DbContext
    {
        private readonly ITenantService _tenantService;

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ITenantService tenantService)
            : base(options)
        {
            _tenantService = tenantService;
        }
        private Guid? CurrentTenantId => _tenantService.TenantId;
        public DbSet<Organization> Organizations => Set<Organization>();
        public DbSet<User> Users => Set<User>();
        public DbSet<Auction> Auctions => Set<Auction>();
        public DbSet<Bid> Bids => Set<Bid>();
        public DbSet<AuctionAuditLog> AuctionAuditLogs => Set<AuctionAuditLog>();
        public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
        public DbSet<AuctionState_Tr> AuctionStates => Set<AuctionState_Tr>();
        public DbSet<BidState_Tr> BidStates => Set<BidState_Tr>();
        public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
                {
                    modelBuilder.Entity(entityType.ClrType)
                        .Property(nameof(BaseEntity.RowVersion))
                        .IsConcurrencyToken();
                }
            }
           
            modelBuilder.Entity<Organization>().HasQueryFilter(e => e.AvailabilityId != Availability.Deleted);

            modelBuilder.Entity<Auction>().HasQueryFilter(e =>
                e.AvailabilityId != Availability.Deleted &&
                (!CurrentTenantId.HasValue || e.OrgId == CurrentTenantId));

            modelBuilder.Entity<User>().HasQueryFilter(e =>
                e.AvailabilityId != Availability.Deleted &&
                (!CurrentTenantId.HasValue || e.OrgId == CurrentTenantId));
            modelBuilder.Entity<AuctionAuditLog>().HasQueryFilter(log =>
    !CurrentTenantId.HasValue || log.Auction.OrgId == CurrentTenantId);

            modelBuilder.Entity<Bid>().HasQueryFilter(e =>
                e.AvailabilityId != Availability.Deleted &&
                (!CurrentTenantId.HasValue || e.Auction.OrgId == CurrentTenantId));


        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var currentTenant = CurrentTenantId;

        
            var auditLogEntries = ChangeTracker.Entries<AuctionAuditLog>()
                .Where(e => e.State == EntityState.Modified || e.State == EntityState.Deleted);

            if (auditLogEntries.Any())
            {
                throw new InvalidOperationException("AuctionAuditLog is append-only and cannot be modified or deleted.");
            }

            foreach (var entry in ChangeTracker.Entries<BaseEntity>())
            {
                switch (entry.State)
                {
                    case EntityState.Added:
                        entry.Entity.CreatedUtc = now;
                        entry.Entity.AvailabilityId = Availability.Active;

                        entry.Entity.RowVersion = Guid.NewGuid().ToByteArray();

                        if (currentTenant.HasValue)
                        {
                            if (currentTenant.HasValue && entry.Entity is IMustHaveTenant tenantEntity)
                            {
                                tenantEntity.OrgId = currentTenant.Value;
                            }
                        }
                        break;

                    case EntityState.Modified:
                        entry.Entity.RowVersion = Guid.NewGuid().ToByteArray();
                        break;

                    case EntityState.Deleted:
                        entry.State = EntityState.Modified;
                        entry.Entity.AvailabilityId = Availability.Deleted;

                        entry.Entity.RowVersion = Guid.NewGuid().ToByteArray();
                        break;
                }
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }
}