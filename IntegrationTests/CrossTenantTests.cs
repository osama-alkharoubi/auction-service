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

public class CrossTenantTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public CrossTenantTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetAuctions_WithCrossTenantToken_ShouldReturnForbidden()
    {
        var organizationA = Guid.NewGuid();
        var organizationB = Guid.NewGuid();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auctions");
        request.Headers.Add("Test-Org-Id", organizationA.ToString());
        request.Headers.Add("Test-User-Id", Guid.NewGuid().ToString());
        request.Headers.Add("Test-Role", "Bidder");
        request.Headers.Add("X-Org-Id", organizationB.ToString());
        request.Headers.Add("Authorization", "Test");
        using var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetAuctions_AsOrgA_CannotReadOrgBAuctionOrSeeItInList()
    {
        var organizationAId = Guid.NewGuid();
        var organizationBId = Guid.NewGuid();
        var userAId = Guid.NewGuid();
        var sellerBId = Guid.NewGuid();
        var auctionBId = Guid.NewGuid();

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var organizationA = new Organization
            {
                Id = organizationAId,
                Name = "Org A"
            };
            var organizationB = new Organization
            {
                Id = organizationBId,
                Name = "Org B"
            };

            dbContext.Organizations.AddRange(organizationA, organizationB);
            dbContext.Users.AddRange(
                new User
                {
                    Id = userAId,
                    OrgId = organizationAId,
                    Organization = organizationA,
                    Email = $"user-a-{userAId:N}@auction.test",
                    PasswordHash = "not-used-by-test",
                    Role = Role.Bidder
                },
                new User
                {
                    Id = sellerBId,
                    OrgId = organizationBId,
                    Organization = organizationB,
                    Email = $"seller-b-{sellerBId:N}@auction.test",
                    PasswordHash = "not-used-by-test",
                    Role = Role.Seller
                });

            var now = DateTime.UtcNow;
            dbContext.Auctions.Add(new Auction
            {
                Id = auctionBId,
                OrgId = organizationBId,
                Organization = organizationB,
                SellerUserId = sellerBId,
                Seller = dbContext.Users.Local.Single(user => user.Id == sellerBId),
                ItemTitle = "Org B private auction",
                Description = "Must not be visible to Org A.",
                StartingPrice = 100m,
                ReserveMinPrice = 100m,
                MinIncrement = 5m,
                StartsUtc = now.AddHours(-1),
                EndsUtc = now.AddDays(1),
                StateId = AuctionState.Live
            });

            await dbContext.SaveChangesAsync();
        }

        using var detailRequest = CreateTenantRequest(
            HttpMethod.Get,
            $"/api/auctions/{auctionBId}/leaderboard",
            organizationAId,
            userAId);
        using var detailResponse = await _client.SendAsync(detailRequest);
        detailResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);

        using var listRequest = CreateTenantRequest(
            HttpMethod.Get,
            "/api/auctions",
            organizationAId,
            userAId);
        using var listResponse = await _client.SendAsync(listRequest);
        listResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        using var listDocument = JsonDocument.Parse(await listResponse.Content.ReadAsStringAsync());
        var returnedAuctionIds = listDocument.RootElement
            .GetProperty("items")
            .EnumerateArray()
            .Select(item => item.GetProperty("id").GetGuid());
        returnedAuctionIds.Should().NotContain(auctionBId);
    }

    private static HttpRequestMessage CreateTenantRequest(
        HttpMethod method,
        string uri,
        Guid organizationId,
        Guid userId)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Add("X-Org-Id", organizationId.ToString());
        request.Headers.Add("Test-Org-Id", organizationId.ToString());
        request.Headers.Add("Test-User-Id", userId.ToString());
        request.Headers.Add("Test-Role", "Bidder");
        request.Headers.Authorization = new AuthenticationHeaderValue("Test");
        return request;
    }
}
