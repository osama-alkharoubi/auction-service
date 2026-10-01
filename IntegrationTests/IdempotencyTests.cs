using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Infrastructure;
using IntegrationTests.Setup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IntegrationTests;

public class IdempotencyTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public IdempotencyTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PlaceBid_WithSameIdempotencyKey_ReplaysOriginalResponseWithoutDuplicateBid()
    {
        var idempotencyKey = Guid.NewGuid();
        var organizationId = SeedData.OrganizationId;
        var bidderUserId = SeedData.BidderUserId;
        var auctionId = SeedData.AuctionId;

        using var firstRequest = CreateBidRequest(auctionId, organizationId, bidderUserId, idempotencyKey);
        using var firstResponse = await _client.SendAsync(firstRequest);
        var firstBody = await firstResponse.Content.ReadAsStringAsync();

        using var replayRequest = CreateBidRequest(auctionId, organizationId, bidderUserId, idempotencyKey);
        using var replayResponse = await _client.SendAsync(replayRequest);
        var replayBody = await replayResponse.Content.ReadAsStringAsync();

        firstResponse.StatusCode.Should().Be(HttpStatusCode.OK,
            "the valid seeded bid should be accepted; response body: {0}", firstBody);
        replayResponse.StatusCode.Should().Be(firstResponse.StatusCode);
        replayBody.Should().Be(firstBody);
        using var originalResponseDocument = JsonDocument.Parse(firstBody);
        var originalBidId = originalResponseDocument.RootElement.GetProperty("bidId").GetGuid();

        var otherTenantId = Guid.NewGuid();
        var otherTenantUserId = Guid.NewGuid();
        using var otherTenantRequest = CreateBidRequest(
            auctionId,
            otherTenantId,
            otherTenantUserId,
            idempotencyKey);
        using var otherTenantResponse = await _client.SendAsync(otherTenantRequest);
        var otherTenantBody = await otherTenantResponse.Content.ReadAsStringAsync();

        otherTenantResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        otherTenantBody.Should().NotContain(originalBidId.ToString());
        otherTenantBody.Should().NotContain("\"bidId\"");

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var matchingBids = await dbContext.Bids
            .IgnoreQueryFilters()
            .CountAsync(bid => bid.IdempotencyKey == idempotencyKey.ToString());
        var matchingRecords = await dbContext.IdempotencyRecords
            .IgnoreQueryFilters()
            .CountAsync(record => record.IdempotencyKey == idempotencyKey.ToString());
        var originalUserRecords = await dbContext.IdempotencyRecords
            .IgnoreQueryFilters()
            .CountAsync(record => record.IdempotencyKey == idempotencyKey.ToString() &&
                                  record.UserId == bidderUserId);

        matchingBids.Should().Be(1);
        matchingRecords.Should().Be(1);
        originalUserRecords.Should().Be(1);
    }

    private static HttpRequestMessage CreateBidRequest(
        Guid auctionId,
        Guid organizationId,
        Guid bidderUserId,
        Guid idempotencyKey)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/auctions/{auctionId}/bids")
        {
            Content = new StringContent(
                "{\"Amount\":250.00}",
                Encoding.UTF8,
                "application/json")
        };

        request.Headers.Add("Idempotency-Key", idempotencyKey.ToString());
        request.Headers.Add("X-Org-Id", organizationId.ToString());
        request.Headers.Add("Test-Org-Id", organizationId.ToString());
        request.Headers.Add("Test-User-Id", bidderUserId.ToString());
        request.Headers.Add("Test-Role", "Bidder");
        request.Headers.Authorization = new AuthenticationHeaderValue("Test");

        return request;
    }
}
