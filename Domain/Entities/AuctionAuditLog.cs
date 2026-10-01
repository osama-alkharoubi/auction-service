using Domain.Enums;

namespace Domain.Entities;

public class AuctionAuditLog
{
    public Guid Id { get; set; }
    public Guid AuctionId { get; set; }
    public Guid ActorUserId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public AuctionState BeforeStateId { get; set; }
    public AuctionState AfterStateId { get; set; }
    public string Metadata { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public Auction Auction { get; set; } = null!;
}
