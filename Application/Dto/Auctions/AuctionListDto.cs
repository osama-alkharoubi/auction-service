namespace Application.Dto.Auctions
{
    public class AuctionListDto
    {
        public Guid Id { get; set; }
        public string ItemTitle { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal StartingPrice { get; set; }
        public decimal ReserveMinPrice { get; set; }
        public decimal MinIncrement { get; set; }
        public DateTime StartsUtc { get; set; }
        public DateTime EndsUtc { get; set; }
        public string StateName { get; set; } = string.Empty;
        public int? TimeRemainingSeconds { get; set; }
        public DateTime CreatedUtc { get; set; }
    }
}
