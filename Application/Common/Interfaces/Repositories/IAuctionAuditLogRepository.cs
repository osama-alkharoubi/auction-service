using Domain.Entities;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Common.Interfaces.Repositories;

public interface IAuctionAuditLogRepository
{
    void Add(AuctionAuditLog entity);
}
