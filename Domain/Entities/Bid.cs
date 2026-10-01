using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class Bid : BaseEntity
{
    public Guid Id { get; set; }
    public Guid AuctionId { get; set; }
    public Guid BidderUserId { get; set; }
    public decimal Amount { get; set; }
    public DateTime PlacedUtc { get; set; } = DateTime.UtcNow;
    public string IdempotencyKey { get; set; } = string.Empty;
    public BidState StateId { get; set; }
    public string? RejectReason { get; set; }

    public Auction Auction { get; set; } = null!;

    public User Bidder { get; set; } = null!;
}
