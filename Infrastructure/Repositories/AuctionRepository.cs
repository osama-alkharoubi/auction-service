using Application.Common.Interfaces.Repositories;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Repositories
{
    public class AuctionRepository : Repository<Auction>, IAuctionRepository
    {
        private readonly ApplicationDbContext _context;

        public AuctionRepository(ApplicationDbContext context) : base(context)
        {
            _context = context;
        }
        public async Task<Auction?> GetByIdWithLockAsync(Guid id, CancellationToken ct)
        {

            var list = await _context.Auctions
          .FromSqlRaw("SELECT * FROM \"auctions\" WHERE \"id\" = {0} FOR UPDATE", id)
          .ToListAsync(ct);

            return list.FirstOrDefault();
        }
        public async Task<Auction?> GetByIdWithCurrentHighBidAsync(Guid auctionId, CancellationToken cancellationToken = default)
        {
            return await _context.Auctions
                .Include(a => a.CurrentHighBid)
                .FirstOrDefaultAsync(a => a.Id == auctionId, cancellationToken);
        }
    }
}
