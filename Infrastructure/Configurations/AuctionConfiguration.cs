using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

public class AuctionConfiguration : IEntityTypeConfiguration<Auction>
{
    public void Configure(EntityTypeBuilder<Auction> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.CreatedUtc).IsRequired();
        builder.Property(e => e.AvailabilityId).IsRequired().HasConversion<int>();
        builder.HasQueryFilter(e => e.AvailabilityId != Availability.Deleted);
        builder.Property(e => e.CurrentHighBidId).IsRequired(false);
        builder.Property(e => e.ItemTitle).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(4000).IsRequired();
        builder.Property(e => e.StartingPrice).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(e => e.ReserveMinPrice).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(e => e.MinIncrement).HasColumnType("decimal(18,2)").IsRequired();
        builder.Property(e => e.StartsUtc).IsRequired();
        builder.Property(e => e.EndsUtc).IsRequired();
        builder.Property(e => e.StateId).IsRequired().HasConversion<int>();
        builder.HasIndex(e => new { e.OrgId, e.AvailabilityId, e.CreatedUtc, e.Id })
            .IsDescending(false, false, true, true)
            .HasDatabaseName("IX_Auctions_Listing_Pagination");
        builder.HasOne(e => e.Organization)
            .WithMany(e => e.Auctions)
            .HasForeignKey(e => e.OrgId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Seller)
            .WithMany(e => e.Auctions)
            .HasForeignKey(e => e.SellerUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(e => e.Bids)
            .WithOne(e => e.Auction)
            .HasForeignKey(e => e.AuctionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.CurrentHighBid)
            .WithOne()
            .HasForeignKey<Auction>(e => e.CurrentHighBidId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
