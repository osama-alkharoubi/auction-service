using Domain.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Common.Interfaces.Repositories
{
    public interface IBidRepository : IRepository<Bid>
    {
        Task<Bid?> GetByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken = default);
    }
}
