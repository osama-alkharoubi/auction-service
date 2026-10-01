using Application.Common.Interfaces.Repositories;
using Domain.Entities;
using System.Threading;
using System.Threading.Tasks;

namespace Infrastructure.Repositories;

public class AuctionAuditLogRepository : IAuctionAuditLogRepository
{
    private readonly ApplicationDbContext _context;

    public AuctionAuditLogRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public void Add(AuctionAuditLog entity)
    {
        _context.AuctionAuditLogs.Add(entity);
    }
}
