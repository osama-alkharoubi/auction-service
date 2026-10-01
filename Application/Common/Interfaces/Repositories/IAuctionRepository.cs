using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Common.Interfaces.Repositories
{
    public interface IAuctionRepository : IRepository<Auction>
    {
        Task<Auction?> GetByIdWithLockAsync(Guid id, CancellationToken ct);
        Task<Auction?> GetByIdWithCurrentHighBidAsync(Guid auctionId, CancellationToken cancellationToken = default);
    }
}
