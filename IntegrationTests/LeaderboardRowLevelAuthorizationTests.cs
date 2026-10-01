using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Infrastructure;
using IntegrationTests.Setup;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IntegrationTests;

public class LeaderboardRowLevelAuthorizationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public LeaderboardRowLevelAuthorizationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetLeaderboard_AsBidder_MasksOtherBiddersEmailAndShowsOwnData()
    {
        var organizationId = SeedData.OrganizationId;
        var sellerUserId = SeedData.SellerUserId;
        var bidderAId = Guid.NewGuid();
        var bidderBId = Guid.NewGuid();
        var auctionId = Guid.NewGuid();
        var bidderABidId = Guid.NewGuid();
        var bidderBBidId = Guid.NewGuid();
        var bidderAEmail = $"bidder-a-{Guid.NewGuid():N}@auction.test";
        var bidderBEmail = $"bidder-b-{Guid.NewGuid():N}@auction.test";
        var now = DateTime.UtcNow;

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            dbContext.Users.AddRange(
                new User
                {
                    Id = bidderAId,
                    OrgId = organizationId,
                    Email = bidderAEmail,
                    PasswordHash = "integration-test-password-hash",
                    Role = Role.Bidder
                },
                new User
                {
                    Id = bidderBId,
                    OrgId = organizationId,
                    Email = bidderBEmail,
                    PasswordHash = "integration-test-password-hash",
                    Role = Role.Bidder
                });

            dbContext.Auctions.Add(new Auction
            {
                Id = auctionId,
                OrgId = organizationId,
                SellerUserId = sellerUserId,
                ItemTitle = "Leaderboard authorization test auction",
                Description = "Isolated auction for leaderboard authorization testing.",
                StartingPrice = 100m,
                ReserveMinPrice = 100m,
                MinIncrement = 5m,
                StartsUtc = now.AddHours(-1),
                EndsUtc = now.AddDays(1),
                StateId = AuctionState.Live
            });

            dbContext.Bids.AddRange(
                new Bid
                {
                    Id = bidderABidId,
                    AuctionId = auctionId,
                    BidderUserId = bidderAId,
                    Amount = 250m,
                    PlacedUtc = now.AddMinutes(-2),
                    IdempotencyKey = Guid.NewGuid().ToString(),
                    StateId = BidState.Winning
                },
                new Bid
                {
                    Id = bidderBBidId,
                    AuctionId = auctionId,
                    BidderUserId = bidderBId,
                    Amount = 200m,
                    PlacedUtc = now.AddMinutes(-1),
                    IdempotencyKey = Guid.NewGuid().ToString(),
                    StateId = BidState.Outbid
                });

            await dbContext.SaveChangesAsync();
        }

        using var request = CreateLeaderboardRequest(auctionId, organizationId, bidderAId, "Bidder");

        using var response = await _client.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        responseBody.Should().NotContain(bidderBEmail);

        using var document = JsonDocument.Parse(responseBody);
        var root = document.RootElement;
        root.GetProperty("auctionId").GetGuid().Should().Be(auctionId);
        var bids = root.GetProperty("bids").EnumerateArray().ToArray();
        bids.Should().HaveCount(2);

        var bidderABid = bids.Single(bid => bid.GetProperty("bidId").GetGuid() == bidderABidId);
        bidderABid.GetProperty("amount").GetDecimal().Should().Be(250m);
        bidderABid.GetProperty("bidderEmail").GetString().Should().Be(bidderAEmail);
        bidderABid.GetProperty("bidderUserId").GetGuid().Should().Be(bidderAId);

        var bidderBBid = bids.Single(bid => bid.GetProperty("bidId").GetGuid() == bidderBBidId);
        var maskedAmount = bidderBBid.GetProperty("amount");
        (maskedAmount.ValueKind == JsonValueKind.Null ||
         maskedAmount.ValueKind == JsonValueKind.Number && maskedAmount.GetDecimal() == 0m)
            .Should()
            .BeTrue("another bidder's amount must be null or the API's zero-value mask, never the real amount");
        bidderBBid.GetProperty("amount").ToString().Should().NotBe("200");
        bidderBBid.GetProperty("bidderEmail").ValueKind.Should().Be(JsonValueKind.Null);
        bidderBBid.GetProperty("bidderUserId").ValueKind.Should().Be(JsonValueKind.Null);
    }

    private static HttpRequestMessage CreateLeaderboardRequest(
        Guid auctionId,
        Guid organizationId,
        Guid userId,
        string role)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/auctions/{auctionId}/leaderboard");
        request.Headers.Add("X-Org-Id", organizationId.ToString());
        request.Headers.Add("Test-Org-Id", organizationId.ToString());
        request.Headers.Add("Test-User-Id", userId.ToString());
        request.Headers.Add("Test-Role", role);
        request.Headers.Authorization = new AuthenticationHeaderValue("Test");
        return request;
    }
}
