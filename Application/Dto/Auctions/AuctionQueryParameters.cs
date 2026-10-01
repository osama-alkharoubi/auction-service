using Domain.Enums;

namespace Application.Dto.Auctions
{
    public class AuctionQueryParameters
    {
        public AuctionState? State { get; set; }
        public DateTime? EndsBefore { get; set; }
        public string? Search { get; set; }
        public string? Cursor { get; set; }
        public int PageSize { get; set; } = 10;
    }
}
