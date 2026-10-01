using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Domain.Enums;
using Infrastructure;
using IntegrationTests.Setup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IntegrationTests;

public class AuctionStateMachineTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuctionStateMachineTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Theory]
    [InlineData(AuctionState.Draft, "Draft", AuctionState.Draft, "Draft")]
    [InlineData(AuctionState.Draft, "Draft", AuctionState.Live, "Live")]
    [InlineData(AuctionState.Draft, "Draft", AuctionState.Closed, "Closed")]
    [InlineData(AuctionState.Draft, "Draft", AuctionState.Settled, "Settled")]
    [InlineData(AuctionState.Scheduled, "Scheduled", AuctionState.Draft, "Draft")]
    [InlineData(AuctionState.Scheduled, "Scheduled", AuctionState.Scheduled, "Scheduled")]
    [InlineData(AuctionState.Scheduled, "Scheduled", AuctionState.Closed, "Closed")]
    [InlineData(AuctionState.Scheduled, "Scheduled", AuctionState.Settled, "Settled")]
    [InlineData(AuctionState.Live, "Live", AuctionState.Draft, "Draft")]
    [InlineData(AuctionState.Live, "Live", AuctionState.Scheduled, "Scheduled")]
    [InlineData(AuctionState.Live, "Live", AuctionState.Live, "Live")]
    [InlineData(AuctionState.Live, "Live", AuctionState.Settled, "Settled")]
    [InlineData(AuctionState.Closed, "Closed", AuctionState.Draft, "Draft")]
    [InlineData(AuctionState.Closed, "Closed", AuctionState.Scheduled, "Scheduled")]
    [InlineData(AuctionState.Closed, "Closed", AuctionState.Live, "Live")]
    [InlineData(AuctionState.Closed, "Closed", AuctionState.Closed, "Closed")]
    [InlineData(AuctionState.Settled, "Settled", AuctionState.Draft, "Draft")]
    [InlineData(AuctionState.Settled, "Settled", AuctionState.Scheduled, "Scheduled")]
    [InlineData(AuctionState.Settled, "Settled", AuctionState.Live, "Live")]
    [InlineData(AuctionState.Settled, "Settled", AuctionState.Closed, "Closed")]
    [InlineData(AuctionState.Settled, "Settled", AuctionState.Settled, "Settled")]
    [InlineData(AuctionState.Cancelled, "Cancelled", AuctionState.Draft, "Draft")]
    [InlineData(AuctionState.Cancelled, "Cancelled", AuctionState.Scheduled, "Scheduled")]
    [InlineData(AuctionState.Cancelled, "Cancelled", AuctionState.Live, "Live")]
    [InlineData(AuctionState.Cancelled, "Cancelled", AuctionState.Closed, "Closed")]
    [InlineData(AuctionState.Cancelled, "Cancelled", AuctionState.Settled, "Settled")]
    public async Task ChangeState_WithInvalidTransition_ReturnsConflict(
        AuctionState fromState,
        string fromStateName,
        AuctionState toState,
        string toStateName)
    {
        var auctionId = Guid.NewGuid();

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var now = DateTime.UtcNow;
            dbContext.Auctions.Add(new Domain.Entities.Auction
            {
                Id = auctionId,
                OrgId = SeedData.OrganizationId,
                SellerUserId = SeedData.SellerUserId,
                ItemTitle = "State machine test auction",
                Description = "Auction created for an isolated state machine test case.",
                StartingPrice = 100m,
                ReserveMinPrice = 100m,
                MinIncrement = 5m,
                StartsUtc = now.AddDays(-1),
                EndsUtc = now.AddDays(1),
                StateId = fromState
            });
            await dbContext.SaveChangesAsync();
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"/api/auctions/{auctionId}/state")
        {
            Content = JsonContent.Create(new { NewState = (int)toState, Reason = (string?)null })
        };

        request.Headers.Add("X-Org-Id", SeedData.OrganizationId.ToString());
        request.Headers.Add("Test-Org-Id", SeedData.OrganizationId.ToString());
        request.Headers.Add("Test-User-Id", SeedData.SellerUserId.ToString());
        request.Headers.Add("Test-Role", "Seller");
        request.Headers.Authorization = new AuthenticationHeaderValue("Test");

        using var response = await _client.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        responseBody.Should().Contain($"from {fromStateName} to {toStateName}");
    }
}
