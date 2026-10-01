using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

public class BidConfiguration : IEntityTypeConfiguration<Bid>
{
    public void Configure(EntityTypeBuilder<Bid> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.CreatedUtc).IsRequired();
        builder.Property(e => e.AvailabilityId).IsRequired().HasConversion<int>();
        builder.HasQueryFilter(e => e.AvailabilityId != Availability.Deleted);

        builder.Property(e => e.Amount).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(e => e.PlacedUtc).IsRequired();
        builder.Property(e => e.IdempotencyKey).HasMaxLength(100).IsRequired();
        builder.Property(e => e.StateId).IsRequired().HasConversion<int>();
        builder.Property(e => e.RejectReason).HasMaxLength(1000);
        builder.HasIndex(e => new { e.AuctionId, e.AvailabilityId, e.Amount, e.PlacedUtc })
       .IsDescending(false, false, true, false)
       .HasDatabaseName("IX_Bids_Leaderboard_Amount");
        builder.HasIndex(e => e.IdempotencyKey).IsUnique();
        builder.HasIndex(e => new { e.BidderUserId, e.PlacedUtc });

        builder.HasOne(e => e.Auction)
            .WithMany(e => e.Bids)
            .HasForeignKey(e => e.AuctionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Bidder)
            .WithMany()
            .HasForeignKey(e => e.BidderUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
