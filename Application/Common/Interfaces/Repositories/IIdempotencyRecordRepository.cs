using Domain.Entities;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Common.Interfaces.Repositories;

public interface IIdempotencyRecordRepository
{
    void Add(IdempotencyRecord entity);
    Task<IdempotencyRecord?> GetByKeyAsync(string key, Guid userId, CancellationToken ct = default);
}
