using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Application.Common.Interfaces.Authentication;
using Application.Dto.Auth;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Infrastructure;
using IntegrationTests.Setup;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace IntegrationTests;

public class RealJwtAuthenticationTests : IClassFixture<RealJwtWebApplicationFactory>
{
    private const string TestPassword = "Real-Jwt-Integration-Test-Password!";
    private readonly RealJwtWebApplicationFactory _factory;

    public RealJwtAuthenticationTests(RealJwtWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task LoginToken_CanAuthenticateAgainstProtectedAuctionsEndpoint()
    {
        const string email = "real-jwt-integration@auction.test";
        var userId = Guid.NewGuid();

        using var client = _factory.CreateClient();
        string passwordHash;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var services = scope.ServiceProvider;
            passwordHash = services.GetRequiredService<IPasswordHasher>().Hash(TestPassword);

            var dbContext = services.GetRequiredService<ApplicationDbContext>();
            dbContext.Users.Add(new User
            {
                Id = userId,
                OrgId = SeedData.OrganizationId,
                Email = email,
                PasswordHash = passwordHash,
                Role = Role.Bidder
            });
            await dbContext.SaveChangesAsync();
        }

        using var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new LoginRequestDto
        {
            Email = email,
            Password = TestPassword
        });

        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var authResponse = await loginResponse.Content.ReadFromJsonAsync<AuthResponseDto>();
        authResponse.Should().NotBeNull();
        authResponse!.AccessToken.Should().NotBeNullOrWhiteSpace();

        using var authenticatedRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/auctions/{SeedData.AuctionId}/leaderboard");
        authenticatedRequest.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", authResponse.AccessToken);
        authenticatedRequest.Headers.Add("X-Org-Id", SeedData.OrganizationId.ToString());

        using var authenticatedResponse = await client.SendAsync(authenticatedRequest);
        authenticatedResponse.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}