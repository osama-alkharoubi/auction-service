using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.CreatedUtc).IsRequired();
        builder.Property(e => e.AvailabilityId).IsRequired().HasConversion<int>();
        builder.HasQueryFilter(e => e.AvailabilityId != Availability.Deleted);

        builder.Property(e => e.Email).HasMaxLength(256).IsRequired();
        builder.HasIndex(e => e.Email).IsUnique();
        builder.Property(e => e.PasswordHash).HasMaxLength(512).IsRequired();
        builder.Property(e => e.Role).IsRequired().HasConversion<int>();

        builder.HasOne(e => e.Organization)
            .WithMany(e => e.Users)
            .HasForeignKey(e => e.OrgId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
