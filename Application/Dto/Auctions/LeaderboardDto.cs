namespace Application.Dto.Auctions;

public class LeaderboardDto
{
    public Guid AuctionId { get; set; }
    public List<LeaderboardBidDto> Bids { get; set; } = new();
}
