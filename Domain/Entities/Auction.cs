using Domain.Common;
using Domain.Enums;

namespace Domain.Entities;

public class Auction : BaseEntity, IMustHaveTenant
{
    public Guid Id { get; set; }
    public Guid OrgId { get; set; }
    public Guid SellerUserId { get; set; }
    public string ItemTitle { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal StartingPrice { get; set; }
    public decimal ReserveMinPrice { get; set; }
    public decimal MinIncrement { get; set; }
    public DateTime StartsUtc { get; set; }
    public DateTime EndsUtc { get; set; }
    public AuctionState StateId { get; set; }
    public Guid? CurrentHighBidId { get; set; }

    public Organization Organization { get; set; } = null!;

    public User Seller { get; set; } = null!;

    public Bid? CurrentHighBid { get; set; }

    public ICollection<Bid> Bids { get; set; } = new HashSet<Bid>();


}
