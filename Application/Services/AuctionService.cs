using Application.Common.Interfaces;
using Application.Common.Interfaces.Repositories;
using Application.Common.Services;
using Application.Dto.Auctions;
using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;
using Domain.Services;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Services
{
    public class AuctionService : IAuctionService
    {
        private readonly IAuctionRepository _auctionRepo;
        private readonly IBidRepository _bidRepo;
        private readonly IAuctionAuditLogRepository _auditLogRepo;
        private readonly IOutboxMessageRepository _outboxRepo;
        private readonly IUnitOfWork _unitOfWork;

        public AuctionService(
            IAuctionRepository auctionRepo,
            IBidRepository bidRepo,
            IAuctionAuditLogRepository auditLogRepo,
            IOutboxMessageRepository outboxRepo,
            IUnitOfWork unitOfWork)
        {
            _auctionRepo = auctionRepo;
            _bidRepo = bidRepo;
            _auditLogRepo = auditLogRepo;
            _outboxRepo = outboxRepo;
            _unitOfWork = unitOfWork;
        }

        public async Task ChangeStateAsync(
         ChangeAuctionStateDto dto,
          CancellationToken ct = default)
        {
            await _unitOfWork.BeginTransactionAsync(ct);

            try
            {
                var auction = await _auctionRepo.GetByIdAsync(dto.AuctionId,ct);

                if (auction == null)
                    throw new KeyNotFoundException("Auction not found.");

                if (!dto.IsAdmin && auction.SellerUserId != dto.ActorUserId)
                    throw new UnauthorizedAccessException("You do not have permission to modify this auction.");
                var now = DateTime.UtcNow;

                if (dto.NewState == AuctionState.Settled && auction.ReserveMinPrice > 0)
                {
                    if (!auction.CurrentHighBidId.HasValue)
                    {
                        throw new InvalidStateTransitionException(
                            auction.StateId.ToString(),
                            dto.NewState.ToString(),
                            "Auction cannot be settled without a high bid meeting the reserve price.");
                    }

                    var highBid = await _bidRepo.GetByIdAsync(auction.CurrentHighBidId.Value, ct);
                    if (highBid == null || highBid.Amount < auction.ReserveMinPrice)
                    {
                        throw new InvalidStateTransitionException(
                            auction.StateId.ToString(),
                            dto.NewState.ToString(),
                            "Auction cannot be settled because the high bid is below the reserve price.");
                    }
                }

                var auditLog = AuctionStateMachine.ChangeState(auction, dto.NewState, dto.ActorUserId, dto.IsAdmin, now, dto.Reason);

                var outboxMessage = new OutboxMessage
                {
                    Id = Guid.NewGuid(),
                    OccurredUtc = now,
                    Type = dto.NewState switch
                    {
                        AuctionState.Live => "AuctionPublished",
                        AuctionState.Closed => "AuctionClosed",
                        AuctionState.Settled => "AuctionSettled",
                        _ => "AuctionStateChanged"
                    },
                    PayloadJson = JsonSerializer.Serialize(new
                    {
                        AuctionId = auction.Id,
                        OldState = auditLog.BeforeStateId,
                        NewState = dto.NewState
                    }),
                    Attempts = 0
                };

                _auditLogRepo.Add(auditLog);
                _outboxRepo.Add(outboxMessage);

                await _unitOfWork.SaveChangesAsync(ct);
                await _unitOfWork.CommitTransactionAsync(ct);
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync(ct);
                throw;
            }
        }
    }
}