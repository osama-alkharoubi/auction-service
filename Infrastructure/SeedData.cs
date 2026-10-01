using Domain.Entities;
using Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure;

public static class SeedData
{
    public static readonly Guid OrganizationId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid SellerUserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid BidderUserId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid AuctionId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    public static readonly Guid BidderUserId2 = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private const string PlaceholderPasswordHash = "$2a$11$rVYIV/oBDF5yK0RwF/ENc.GzEdb8Q3UrDzVmCydiH.dWDwyeOTgJ2";

    public static async Task SeedAsync(ApplicationDbContext context)
    {
        var organizationExists = await context.Organizations
            .IgnoreQueryFilters()
            .AnyAsync(organization => organization.Id == OrganizationId);

        var organization = organizationExists
            ? await context.Organizations.IgnoreQueryFilters()
                .SingleAsync(organization => organization.Id == OrganizationId)
            : new Organization { Id = OrganizationId };

        organization.Name = "Auction Demo Organization";
        organization.IsActive = true;
        organization.AvailabilityId = Availability.Active;

        if (!organizationExists)
            context.Organizations.Add(organization);

        var sellerExists = await context.Users
            .IgnoreQueryFilters()
            .AnyAsync(user => user.Id == SellerUserId);

        var seller = sellerExists
            ? await context.Users.IgnoreQueryFilters()
                .SingleAsync(user => user.Id == SellerUserId)
            : new User { Id = SellerUserId };

        if (!sellerExists)
        {
            seller.Email = "seller@auction.test";
            seller.PasswordHash = PlaceholderPasswordHash;
            seller.Role = Role.Seller;
            seller.OrgId = OrganizationId;
            seller.Organization = organization;
            context.Users.Add(seller);
        }

        var bidderExists = await context.Users
            .IgnoreQueryFilters()
            .AnyAsync(user => user.Id == BidderUserId);

        var bidder = bidderExists
            ? await context.Users.IgnoreQueryFilters()
                .SingleAsync(user => user.Id == BidderUserId)
            : new User { Id = BidderUserId };

        if (!bidderExists)
        {
            bidder.Email = "bidder@auction.test";
            bidder.PasswordHash = PlaceholderPasswordHash;
            bidder.Role = Role.Bidder;
            bidder.OrgId = OrganizationId;
            bidder.Organization = organization;
            context.Users.Add(bidder);
        }
        var bidder2Exists = await context.Users.IgnoreQueryFilters().AnyAsync(u => u.Id == BidderUserId2);
        var bidder2 = bidder2Exists ? await context.Users.IgnoreQueryFilters().SingleAsync(u => u.Id == BidderUserId2) : new User { Id = BidderUserId2 };

        if (!bidder2Exists)
        {
            bidder2.Email = "bidder2@auction.test";
            bidder2.PasswordHash = PlaceholderPasswordHash;
            bidder2.Role = Role.Bidder;
            bidder2.OrgId = OrganizationId;
            bidder2.Organization = organization;
            context.Users.Add(bidder2);
        }
        var auctionExists = await context.Auctions
            .IgnoreQueryFilters()
            .AnyAsync(auction => auction.Id == AuctionId);

        if (!auctionExists)
        {
            var now = DateTime.UtcNow;
            context.Auctions.Add(new Auction
            {
                Id = AuctionId,
                OrgId = OrganizationId,
                Organization = organization,
                SellerUserId = SellerUserId,
                Seller = seller,
                ItemTitle = "Demo Auction Item",
                Description = "A seeded auction for local development and API testing.",
                StartingPrice = 100.00m,
                ReserveMinPrice = 150.00m,
                MinIncrement = 5.00m,
                StartsUtc = now.AddHours(-1),
                EndsUtc = now.AddDays(7),
                StateId = AuctionState.Live,
                CurrentHighBidId = null
            });
        }

        await context.SaveChangesAsync();
    }
}
