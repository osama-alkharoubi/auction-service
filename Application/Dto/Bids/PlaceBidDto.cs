using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Dto.Bids
{
    public class PlaceBidDto
    {
        public Guid AuctionId { get; set; }
        public Guid BidderUserId { get; set; }
        public decimal Amount { get; set; }
        public string IdempotencyKey { get; set; } = string.Empty;
    }
}
