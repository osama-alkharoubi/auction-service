using Application.Common.Interfaces;
using Application.Common.Interfaces.Repositories;
using Application.Common.Services;
using Application.Dto.Bids;
using Domain.Entities;
using Domain.Enums;
using System.Text.Json;

namespace Application.Services;

public class BidService : IBidService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAuctionRepository _auctionRepo;
    private readonly IBidRepository _bidRepo;
    private readonly IIdempotencyRecordRepository _idempotencyRepo;
    private readonly IOutboxMessageRepository _outboxRepo;
    private readonly IAuctionAuditLogRepository _auditLogRepo;

    public BidService(
        IUnitOfWork unitOfWork,
        IAuctionRepository auctionRepo,
        IBidRepository bidRepo,
        IIdempotencyRecordRepository idempotencyRepo,
        IOutboxMessageRepository outboxRepo,
        IAuctionAuditLogRepository auditLogRepo)
    {
        _unitOfWork = unitOfWork;
        _auctionRepo = auctionRepo;
        _bidRepo = bidRepo;
        _idempotencyRepo = idempotencyRepo;
        _outboxRepo = outboxRepo;
        _auditLogRepo = auditLogRepo;
    }

    public async Task<PlaceBidResponseDto> PlaceBidAsync(PlaceBidDto dto, CancellationToken ct = default)
    {
        if (await GetCachedResponseAsync(dto.IdempotencyKey,dto.BidderUserId, ct) is { } cachedResponse)
            return cachedResponse;

        await _unitOfWork.BeginTransactionAsync(ct);

        try
        {
            var auction = await GetAuctionAsync(dto.AuctionId, ct);
            var existingRecord = await _idempotencyRepo.GetByKeyAsync(dto.IdempotencyKey,dto.BidderUserId, ct);
            if (existingRecord != null)
            {
                await _unitOfWork.CommitTransactionAsync(ct);
                var responseDto = JsonSerializer.Deserialize<PlaceBidResponseDto>(existingRecord.ResponseBodyJson)!;
                responseDto.StatusCode = existingRecord.StatusCode;
                return responseDto;
            }
            var currentHighBid = await GetCurrentHighBidAsync(auction.CurrentHighBidId, ct);

            var now = DateTime.UtcNow;
            var bid = InitializeBid(dto, auction.Id, now);
            var response = new PlaceBidResponseDto { BidId = bid.Id };

            string? rejectReason = ValidateBusinessRules(auction, currentHighBid, dto, now);

            if (rejectReason != null)
                ProcessRejectedBid(bid, response, auction, rejectReason, dto.BidderUserId);
            else
                ProcessAcceptedBid(bid, response, auction, currentHighBid, dto.BidderUserId, now);

            _bidRepo.Add(bid);
            SaveIdempotencyRecord(dto.IdempotencyKey,dto.BidderUserId, response, now);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitTransactionAsync(ct);

            return response;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(ct);
            throw;
        }
    }

    private async Task<PlaceBidResponseDto?> GetCachedResponseAsync(string idempotencyKey,Guid userId, CancellationToken ct)
    {
        var record = await _idempotencyRepo.GetByKeyAsync(idempotencyKey, userId, ct);
        if (record == null) return null;

        var response = JsonSerializer.Deserialize<PlaceBidResponseDto>(record.ResponseBodyJson)!;
        response.StatusCode = record.StatusCode;
        return response;
    }
    private async Task<Auction> GetAuctionAsync(Guid auctionId, CancellationToken ct)
    {
        var auction = await _auctionRepo.GetByIdWithLockAsync(auctionId, ct)
        ?? throw new KeyNotFoundException("Auction not found.");
        return auction;
    }

    private async Task<Bid?> GetCurrentHighBidAsync(Guid? currentHighBidId, CancellationToken ct)
    {
        return currentHighBidId.HasValue
            ? await _bidRepo.GetByIdAsync(currentHighBidId.Value, ct)
            : null;
    }

    private Bid InitializeBid(PlaceBidDto dto, Guid auctionId, DateTime now)
    {
        return new Bid
        {
            Id = Guid.NewGuid(),
            AuctionId = auctionId,
            BidderUserId = dto.BidderUserId,
            Amount = dto.Amount,
            PlacedUtc = now,
            IdempotencyKey = dto.IdempotencyKey
        };
    }
    private void ProcessRejectedBid(Bid bid, PlaceBidResponseDto response, Auction auction, string reason, Guid bidderId)
    {
        bid.StateId = BidState.Rejected;
        bid.RejectReason = reason;

        response.State = BidState.Rejected;
        response.RejectReason = reason;
        response.StatusCode = 400;

        AddAuditLog(auction, bidderId, $"Bid rejected: {reason}", bid, "Rejected");
    }

    private void ProcessAcceptedBid(Bid bid, PlaceBidResponseDto response, Auction auction, Bid? currentHighBid, Guid bidderId, DateTime now)
    {
        bid.StateId = BidState.Winning;
        response.State = BidState.Winning;
        response.StatusCode = 200;

        if (currentHighBid != null) currentHighBid.StateId = BidState.Outbid;
        auction.CurrentHighBidId = bid.Id;

        _outboxRepo.Add(new OutboxMessage
        {
            Id = Guid.NewGuid(),
            OccurredUtc = now,
            Type = "BidAccepted",
            Attempts = 0,
            PayloadJson = JsonSerializer.Serialize(new { BidId = bid.Id, AuctionId = auction.Id, Amount = bid.Amount })
        });

        AddAuditLog(auction, bidderId, $"Bid accepted with amount {bid.Amount}", bid, "Accepted");
    }

    private void SaveIdempotencyRecord(string key,Guid userId, PlaceBidResponseDto response, DateTime now)
    {
        _idempotencyRepo.Add(new IdempotencyRecord
        {
            IdempotencyKey = key,
            StatusCode = response.StatusCode,
            ResponseBodyJson = JsonSerializer.Serialize(response),
            CreatedUtc = now,
            UserId = userId
        });
    }

    private void AddAuditLog(Auction auction, Guid userId, string reason, Bid bid, string status)
    {
        _auditLogRepo.Add(new AuctionAuditLog
        {
            Id = Guid.NewGuid(),
            AuctionId = auction.Id,
            ActorUserId = userId,
            Reason = reason,
            BeforeStateId = auction.StateId,
            AfterStateId = auction.StateId,
            Metadata = JsonSerializer.Serialize(new { BidId = bid.Id, Amount = bid.Amount, Status = status })
        });
    }

    private string? ValidateBusinessRules(Auction auction, Bid? currentHighBid, PlaceBidDto dto, DateTime now)
    {
        if (auction.StateId != AuctionState.Live || now < auction.StartsUtc || now >= auction.EndsUtc)
            return "Auction is not currently Live.";

        if (dto.BidderUserId == auction.SellerUserId)
            return "Seller cannot place a bid on their own auction.";

        if (currentHighBid != null && currentHighBid.BidderUserId == dto.BidderUserId)
            return "Bidder cannot outbid their own currently winning bid.";

        decimal minimumAllowed = currentHighBid != null
            ? currentHighBid.Amount + auction.MinIncrement
            : auction.StartingPrice;

        if (dto.Amount < minimumAllowed)
            return $"Amount must be at least {minimumAllowed}.";

        return null;
    }
}