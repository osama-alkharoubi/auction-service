using Application.Common.Interfaces;
using Application.Common.Services;
using Application.Dto.Bids;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using FluentValidation;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace Api.Controllers;

[ApiController]
[Authorize(Roles = "Bidder")]
[Route("api/auctions/{auctionId:guid}/bids")]
public class BidsController : ControllerBase
{
    private readonly IBidService _bidService;
    private readonly ITenantContext _tenantContext;
    private readonly IValidator<PlaceBidDto> _placeBidValidator;

    public BidsController(
        IBidService bidService,
        ITenantContext tenantContext,
        IValidator<PlaceBidDto> placeBidValidator)
    {
        _bidService = bidService;
        _tenantContext = tenantContext;
        _placeBidValidator = placeBidValidator;
    }

    [HttpPost]
    [EnableRateLimiting("BidsPerTenantPolicy")]
    [ProducesResponseType(typeof(PlaceBidResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(PlaceBidResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(PlaceBidResponseDto), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<PlaceBidResponseDto>> PlaceBid(
        Guid auctionId,
        [FromBody] PlaceBidRequest request,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken ct)
    {
   
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Missing Header",
                Detail = "Idempotency-Key header is required."
            });
        }

        if (_tenantContext.OrgId == Guid.Empty)
            return Forbid();

        var userIdValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdValue, out var bidderUserId))
            return Unauthorized();

        var dto = new PlaceBidDto
        {
            AuctionId = auctionId,
            BidderUserId = bidderUserId,
            Amount = request.Amount,
            IdempotencyKey = idempotencyKey
        };

        var validationResult = await _placeBidValidator.ValidateAsync(dto, ct);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.Errors
                .GroupBy(error => error.PropertyName)
                .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray());
            return BadRequest(new ValidationProblemDetails(errors)
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "One or more validation errors occurred."
            });
        }

        var response = await _bidService.PlaceBidAsync(dto, ct);

        return response.StatusCode switch
        {
            StatusCodes.Status200OK => Ok(response),
            StatusCodes.Status201Created => CreatedAtAction(nameof(PlaceBid), new { auctionId }, response),
            StatusCodes.Status400BadRequest => BadRequest(response),
            _ => StatusCode(response.StatusCode, response)
        };
    }
}

public sealed class PlaceBidRequest
{
    [Required]
    [Range(0.01, 100_000_000.00, ErrorMessage = "Amount must be greater than zero.")]
    public decimal Amount { get; set; }
}