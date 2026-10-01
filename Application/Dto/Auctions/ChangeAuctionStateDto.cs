using Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Dto.Auctions
{
    public class ChangeAuctionStateDto
    {
        public Guid AuctionId { get; set; }
        public AuctionState NewState { get; set; }
        public Guid ActorUserId { get; set; }
        public bool IsAdmin { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}
