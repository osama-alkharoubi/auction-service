using Application.Dto.Auctions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Common.Interfaces
{
    public interface IAuctionReadQueries
    {
        Task<PagedResultDto<AuctionListDto>> GetAuctionsAsync(
            AuctionQueryParameters parameters,
            string language,
            Guid orgId,
            Guid currentUserId,
            string currentUserRole,
            CancellationToken ct);

       Task<LeaderboardDto?> GetLeaderboardAsync(
            Guid auctionId,
            Guid currentUserId,
            string currentUserRole,
            Guid orgId,
            string language,
            int top,
            CancellationToken ct);
    }
}
