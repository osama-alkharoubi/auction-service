using System.Security.Claims;
using Application.Common.Interfaces;
using Application.Dto.Auctions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Application.Common.Services;
namespace Api.Controllers;

[ApiController]
[Authorize]
[Route("api/auctions")]
public class AuctionsController : ControllerBase
{
    private readonly IAuctionReadQueries _auctionReadQueries;
    private readonly ITenantContext _tenantContext;
    private readonly IAuctionService _auctionService;

    public AuctionsController(IAuctionReadQueries auctionReadQueries,  IAuctionService auctionService, ITenantContext tenantContext)
    {
        _auctionReadQueries = auctionReadQueries;
        _tenantContext = tenantContext;
        _auctionService = auctionService;   
    }

    public record ChangeStateApiRequest(int NewState, string? Reason);



    [HttpPatch("{id:guid}/state")]
    [Authorize(Roles = "Admin,Seller")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangeState(
        Guid id,
        [FromBody] ChangeStateApiRequest request, 
        CancellationToken ct)
    {
    

        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId))
            return Unauthorized();

        var currentUserRole = User.FindFirstValue(ClaimTypes.Role);
        bool isAdmin = currentUserRole == "Admin";

        var dto = new ChangeAuctionStateDto
        {
            AuctionId = id,
            NewState = (Domain.Enums.AuctionState)request.NewState,
            Reason = request.Reason ?? string.Empty,     
            ActorUserId = currentUserId, 
            IsAdmin = isAdmin           
        };

        await _auctionService.ChangeStateAsync(dto, ct);

        return NoContent();
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<AuctionListDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResultDto<AuctionListDto>>> GetAuctions(
        [FromQuery] AuctionQueryParameters parameters,
        CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId))
            return Unauthorized();

        var currentUserRole = User.FindFirstValue(ClaimTypes.Role);
        if (string.IsNullOrWhiteSpace(currentUserRole))
            return Forbid();

        var language = Request.Headers["Accept-Language"].ToString();
        var result = await _auctionReadQueries.GetAuctionsAsync(
            parameters,
            language,
            _tenantContext.OrgId,
            currentUserId,
            currentUserRole,
            ct);

        return Ok(result);
    }

    [HttpGet("{id:guid}/leaderboard")]
    [ProducesResponseType(typeof(LeaderboardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LeaderboardDto>> GetLeaderboard(
        Guid id,
        [FromQuery] int top = 10,
        CancellationToken ct = default)
    {
   
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var currentUserId))
            return Unauthorized();

        var currentUserRole = User.FindFirstValue(ClaimTypes.Role);
        if (string.IsNullOrWhiteSpace(currentUserRole))
            return Forbid();

        var language = Request.Headers["Accept-Language"].ToString();
        var result = await _auctionReadQueries.GetLeaderboardAsync(
            id,
            currentUserId,
            currentUserRole,
            _tenantContext.OrgId,
            language,
            top,
            ct);

        return result is null ? NotFound() : Ok(result);
    }
}
