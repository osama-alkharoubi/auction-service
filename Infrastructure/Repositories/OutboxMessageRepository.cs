using Application.Common.Interfaces.Repositories;
using Domain.Entities;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Repositories;

public class OutboxMessageRepository : IOutboxMessageRepository
{
    private readonly ApplicationDbContext _context;

    public OutboxMessageRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public void Add(OutboxMessage entity)
    {
        _context.OutboxMessages.Add(entity);
    }
}
