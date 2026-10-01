using Application.Common.Interfaces.Repositories;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Repositories;

public class IdempotencyRecordRepository : IIdempotencyRecordRepository
{
    private readonly ApplicationDbContext _context;

    public IdempotencyRecordRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public void Add(IdempotencyRecord entity)
    {
        _context.IdempotencyRecords.Add(entity);
    }

    public async Task<IdempotencyRecord?> GetByKeyAsync(string idempotencyKey, Guid userId, CancellationToken cancellationToken = default)
    {
        return await _context.IdempotencyRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(record => record.IdempotencyKey == idempotencyKey && record.UserId == userId, cancellationToken);
    }
}
