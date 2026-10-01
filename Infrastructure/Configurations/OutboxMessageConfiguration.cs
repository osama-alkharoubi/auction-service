using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.OccurredUtc).IsRequired();
        builder.Property(e => e.Type).HasMaxLength(200).IsRequired();
        builder.Property(e => e.PayloadJson).HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.Attempts).IsRequired();
        builder.Property(e => e.ProcessedUtc).IsRequired(false);
        builder.HasIndex(e => e.OccurredUtc)
        .HasFilter("\"processed_utc\" IS NULL");
    
    }
}
