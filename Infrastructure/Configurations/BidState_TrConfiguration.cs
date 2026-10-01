using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

public class BidState_TrConfiguration : IEntityTypeConfiguration<BidState_Tr>
{
    public void Configure(EntityTypeBuilder<BidState_Tr> builder)
    {
        builder.HasKey(e => new { e.StateId, e.Language });
        builder.Property(e => e.StateId).IsRequired().HasConversion<int>();
        builder.Property(e => e.Language).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();
        builder.HasData(
            new BidState_Tr { StateId = BidState.Accepted, Language = "en", Name = "Accepted" },
            new BidState_Tr { StateId = BidState.Accepted, Language = "ar", Name = "مقبول" },
            new BidState_Tr { StateId = BidState.Rejected, Language = "en", Name = "Rejected" },
            new BidState_Tr { StateId = BidState.Rejected, Language = "ar", Name = "مرفوض" },
            new BidState_Tr { StateId = BidState.Outbid, Language = "en", Name = "Outbid" },
            new BidState_Tr { StateId = BidState.Outbid, Language = "ar", Name = "تم تجاوزه" },
            new BidState_Tr { StateId = BidState.Winning, Language = "en", Name = "Winning" },
            new BidState_Tr { StateId = BidState.Winning, Language = "ar", Name = "فائز" }
        );
    }
}
