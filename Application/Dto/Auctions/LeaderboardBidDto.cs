namespace Application.Dto.Auctions;

public class LeaderboardBidDto
{
    public Guid BidId { get; set; }
    public decimal? Amount { get; set; }
    public DateTime PlacedUtc { get; set; }
    public string BidStateName { get; set; } = null!;
    public string BidderEmail { get; set; } = null!;
    public Guid? BidderUserId { get; set; }
}
