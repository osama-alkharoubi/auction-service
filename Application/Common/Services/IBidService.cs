using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Application.Dto.Bids;
namespace Application.Common.Services
{
    public interface IBidService
    {
        Task<PlaceBidResponseDto> PlaceBidAsync(PlaceBidDto dto, CancellationToken ct = default);
    }
}
