using System.Net.Http.Headers;
using System.Net.Http.Json;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Infrastructure;
using IntegrationTests.Setup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IntegrationTests;

public class BidConcurrencyTests : IClassFixture<CustomWebApplicationFactory>
{
    private const int ConcurrentRequestCount = 50;
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public BidConcurrencyTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PlaceBid_WithFiftyConcurrentRequests_LeavesExactlyOneWinningBid()
    {
        var organizationId = SeedData.OrganizationId;
        var auctionId = Guid.NewGuid();
        var bidderIds = Enumerable.Range(0, ConcurrentRequestCount)
            .Select(_ => Guid.NewGuid())
            .ToArray();
        var now = DateTime.UtcNow;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            dbContext.Users.AddRange(bidderIds.Select((bidderId, index) => new User
            {
                Id = bidderId,
                OrgId = organizationId,
                Email = $"concurrency-bidder-{Guid.NewGuid():N}@auction.test",
                PasswordHash = "integration-test-password-hash",
                Role = Role.Bidder
            }));

            dbContext.Auctions.Add(new Auction
            {
                Id = auctionId,
                OrgId = organizationId,
                SellerUserId = SeedData.SellerUserId,
                ItemTitle = "Concurrent bid test auction",
                Description = "Live auction for concurrent bid integration testing.",
                StartingPrice = 100m,
                ReserveMinPrice = 100m,
                MinIncrement = 5m,
                StartsUtc = now.AddHours(-1),
                EndsUtc = now.AddHours(1),
                StateId = AuctionState.Live
            });

            await dbContext.SaveChangesAsync();
        }

        var idempotencyKeys = Enumerable.Range(0, ConcurrentRequestCount)
            .Select(_ => Guid.NewGuid().ToString())
            .ToArray();
        idempotencyKeys.Distinct().Should().HaveCount(ConcurrentRequestCount);

        var requests = bidderIds.Select((bidderId, index) => SendBidAsync(
            auctionId,
            organizationId,
            bidderId,
            idempotencyKeys[index],
            100m + (index * 5m))).ToArray();
        var responses = await Task.WhenAll(requests);
        responses.Should().HaveCount(ConcurrentRequestCount);
        responses.Select(response => response.StatusCode)
            .Should()
            .OnlyContain(statusCode => statusCode == System.Net.HttpStatusCode.OK ||
                                      statusCode == System.Net.HttpStatusCode.BadRequest,
                "each valid request must either be accepted or rejected by the bid rules");
        responses
            .Where(response => (int)response.StatusCode >= 500)
            .Select(response => response.StatusCode)
            .Should()
            .BeEmpty("concurrent bid requests should not produce server errors");

        foreach (var response in responses)
            response.Dispose();

        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var auction = await verificationDb.Auctions
            .IgnoreQueryFilters()
            .SingleAsync(item => item.Id == auctionId);
        var winningBids = await verificationDb.Bids
            .IgnoreQueryFilters()
            .Where(bid => bid.AuctionId == auctionId && bid.StateId == BidState.Winning)
            .ToListAsync();
        var persistedRequestBids = await verificationDb.Bids
            .IgnoreQueryFilters()
            .Where(bid => bid.AuctionId == auctionId && idempotencyKeys.Contains(bid.IdempotencyKey))
            .Select(bid => bid.IdempotencyKey)
            .ToListAsync();

        persistedRequestBids.Should().HaveCount(ConcurrentRequestCount);
        persistedRequestBids.Distinct().Should().HaveCount(ConcurrentRequestCount);
        winningBids.Should().ContainSingle();
        auction.CurrentHighBidId.Should().Be(winningBids[0].Id);
    }

    private async Task<HttpResponseMessage> SendBidAsync(
        Guid auctionId,
        Guid organizationId,
        Guid bidderId,
        string idempotencyKey,
        decimal amount)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/auctions/{auctionId}/bids")
        {
            Content = JsonContent.Create(new { Amount = amount })
        };

        request.Headers.Add("Idempotency-Key", idempotencyKey);
        request.Headers.Add("X-Org-Id", organizationId.ToString());
        request.Headers.Add("Test-Org-Id", organizationId.ToString());
        request.Headers.Add("Test-User-Id", bidderId.ToString());
        request.Headers.Add("Test-Role", "Bidder");
        request.Headers.Authorization = new AuthenticationHeaderValue("Test");

        return await _client.SendAsync(request);
    }
}
