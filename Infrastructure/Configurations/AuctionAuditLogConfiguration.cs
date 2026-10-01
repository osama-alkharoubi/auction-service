using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

public class AuctionAuditLogConfiguration : IEntityTypeConfiguration<AuctionAuditLog>
{
    public void Configure(EntityTypeBuilder<AuctionAuditLog> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Reason).HasMaxLength(1000).IsRequired(false);
        builder.Property(e => e.Metadata).HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.BeforeStateId).IsRequired().HasConversion<int>();
        builder.Property(e => e.AfterStateId).IsRequired().HasConversion<int>();
        builder.Property(e => e.CreatedAt)
    .IsRequired()
    .HasDefaultValueSql("NOW() AT TIME ZONE 'UTC'")
        .ValueGeneratedOnAdd();
        builder.HasIndex(e => new { e.AuctionId, e.Id });
        builder.HasOne(e => e.Auction)
            .WithMany()
            .HasForeignKey(e => e.AuctionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(e => e.ActorUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
