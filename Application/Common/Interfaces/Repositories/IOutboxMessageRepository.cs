using Domain.Entities;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Common.Interfaces.Repositories;

public interface IOutboxMessageRepository
{
    void Add(OutboxMessage entity);
}
