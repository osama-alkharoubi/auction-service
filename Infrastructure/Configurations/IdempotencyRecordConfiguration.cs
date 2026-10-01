using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

public class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.HasKey(e => new { e.IdempotencyKey, e.UserId });
        builder.Property(e => e.IdempotencyKey).HasMaxLength(100).IsRequired();
        builder.Property(e => e.StatusCode).IsRequired();
        builder.Property(e => e.ResponseBodyJson).HasColumnType("jsonb").IsRequired();
        builder.Property(e => e.CreatedUtc).IsRequired();
        
    }
}
