using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Infrastructure.Configurations;

public class AuctionState_TrConfiguration : IEntityTypeConfiguration<AuctionState_Tr>
{
    public void Configure(EntityTypeBuilder<AuctionState_Tr> builder)
    {
        builder.HasKey(e => new { e.StateId, e.Language });
        builder.Property(e => e.StateId).IsRequired().HasConversion<int>();
        builder.Property(e => e.Language).HasMaxLength(10).IsRequired();
        builder.Property(e => e.Name).HasMaxLength(100).IsRequired();

        builder.HasData(
            new AuctionState_Tr { StateId = AuctionState.Draft, Language = "en", Name = "Draft" },
            new AuctionState_Tr { StateId = AuctionState.Draft, Language = "ar", Name = "مسودة" },
            new AuctionState_Tr { StateId = AuctionState.Scheduled, Language = "en", Name = "Scheduled" },
            new AuctionState_Tr { StateId = AuctionState.Scheduled, Language = "ar", Name = "مجدول" },
            new AuctionState_Tr { StateId = AuctionState.Live, Language = "en", Name = "Live" },
            new AuctionState_Tr { StateId = AuctionState.Live, Language = "ar", Name = "نشط" },
            new AuctionState_Tr { StateId = AuctionState.Closed, Language = "en", Name = "Closed" },
            new AuctionState_Tr { StateId = AuctionState.Closed, Language = "ar", Name = "مغلق" },
            new AuctionState_Tr { StateId = AuctionState.Settled, Language = "en", Name = "Settled" },
            new AuctionState_Tr { StateId = AuctionState.Settled, Language = "ar", Name = "تمت التسوية" },
            new AuctionState_Tr { StateId = AuctionState.Cancelled, Language = "en", Name = "Cancelled" },
            new AuctionState_Tr { StateId = AuctionState.Cancelled, Language = "ar", Name = "ملغى" }
        );
    }
}
