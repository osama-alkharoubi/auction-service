using Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Dto.Bids
{
    public class PlaceBidResponseDto
    {
        public Guid BidId { get; set; }
        public BidState State { get; set; }
        public string? RejectReason { get; set; }
        public int StatusCode { get; set; }
    }
}
