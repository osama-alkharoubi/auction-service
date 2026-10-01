using Application.Dto.Auctions;
using Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Common.Services
{
    public interface IAuctionService
    {
        Task ChangeStateAsync(
       ChangeAuctionStateDto dto,
         CancellationToken ct = default);
    }
}
