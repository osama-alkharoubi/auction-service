using Application.Common.Interfaces;
using Application.Dto.Auctions;
using CloudinaryDotNet;
using Dapper;
using Domain.Enums;
using Microsoft.Extensions.Configuration;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Data
{
    public class AuctionReadQueries : IAuctionReadQueries
    {
        private readonly string _connectionString;
        
        public AuctionReadQueries(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")
                                ?? throw new InvalidOperationException("Connection string not found.");
        }

        public async Task<PagedResultDto<AuctionListDto>> GetAuctionsAsync(
            AuctionQueryParameters parameters,
            string language,
            Guid orgId,
            Guid currentUserId,
            string currentUserRole,
            CancellationToken ct)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            var langParam = string.IsNullOrWhiteSpace(language) || language.Length < 2 ? "en" : language[..2].ToLower();


            var effectivePageSize = parameters.PageSize switch
            {
                <= 0 => 10,
                > 100 => 100,
                _ => parameters.PageSize
            };
            var sqlBuilder = new SqlBuilder();

           
            sqlBuilder.Where("a.org_id = @OrgId");
            sqlBuilder.Where("a.availability_id = 1");

            if (parameters.State.HasValue)
            {
                sqlBuilder.Where("a.state_id = @State");
            }

            if (parameters.EndsBefore.HasValue)
            {
                sqlBuilder.Where("a.ends_utc < @EndsBefore");
            }

            if (!string.IsNullOrWhiteSpace(parameters.Search))
            {
         
                sqlBuilder.Where("(a.item_title ILIKE @Search OR a.description ILIKE @Search)");
            }

     
            DateTime? cursorCreatedUtc = null;
            Guid? cursorId = null;

            if (!string.IsNullOrWhiteSpace(parameters.Cursor))
            {
                var parts = parameters.Cursor.Split('_');
                if (parts.Length == 2 &&
                   DateTime.TryParse(parts[0], System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal, out var parsedDate) &&
                    Guid.TryParse(parts[1], out var parsedId))
                {
                    cursorCreatedUtc = parsedDate;
                    cursorId = parsedId;

                   
                    sqlBuilder.Where("(a.created_utc, a.id) < (@CursorCreatedUtc, @CursorId)");
                }
            }

            var template = sqlBuilder.AddTemplate(@"
            SELECT 
                a.id AS ""Id"", 
                a.item_title AS ""ItemTitle"", 
                a.description AS ""Description"", 
                a.starting_price AS ""StartingPrice"", 
              CASE 
    WHEN @Role = 'Admin' THEN a.reserve_min_price
    WHEN @Role = 'Seller' AND a.seller_user_id = @CurrentUserId THEN a.reserve_min_price
    ELSE NULL
END AS ""ReserveMinPrice"", 
                a.min_increment AS ""MinIncrement"", 
                a.starts_utc AS ""StartsUtc"", 
                a.ends_utc AS ""EndsUtc"", 
                COALESCE(t.name, en_t.name) AS ""StateName"",
                GREATEST(0, EXTRACT(EPOCH FROM (a.ends_utc - NOW())))::int AS ""TimeRemainingSeconds"",
                a.created_utc AS ""CreatedUtc""
            FROM auctions a
            LEFT JOIN auction_states t ON a.state_id = t.state_id AND t.language = @Language
            LEFT JOIN auction_states en_t ON a.state_id = en_t.state_id AND en_t.language = 'en'
            /**where**/
            ORDER BY a.created_utc DESC, a.id DESC
            LIMIT @PageSize;
        ");

            var dynParams = new DynamicParameters();
            dynParams.Add("OrgId", orgId);
            dynParams.Add("CurrentUserId", currentUserId);
            dynParams.Add("Role", currentUserRole);
            dynParams.Add("Language", langParam);
            dynParams.Add("PageSize", effectivePageSize);
            if (parameters.State.HasValue) dynParams.Add("State", (int)parameters.State.Value);
            if (parameters.EndsBefore.HasValue) dynParams.Add("EndsBefore", parameters.EndsBefore.Value.ToUniversalTime());
            if (!string.IsNullOrWhiteSpace(parameters.Search)) dynParams.Add("Search", $"%{parameters.Search}%");
            if (cursorCreatedUtc.HasValue)
            {
                dynParams.Add("CursorCreatedUtc", cursorCreatedUtc.Value);
                dynParams.Add("CursorId", cursorId!.Value);
            }

           
            var items = (await connection.QueryAsync<AuctionListDto>(
                new CommandDefinition(template.RawSql, dynParams, cancellationToken: ct)
            )).AsList();

            string? nextCursor = null;
            if (items.Count == effectivePageSize)
            {
                var lastItem = items.Last();
                nextCursor = $"{lastItem.CreatedUtc:O}_{lastItem.Id}";
            }

            return new PagedResultDto<AuctionListDto>
            {
                Items = items,
                NextCursor = nextCursor
            };
        }

        public async Task<LeaderboardDto?> GetLeaderboardAsync(
    Guid auctionId,
    Guid currentUserId,
    string currentUserRole,
    Guid orgId,
    string language,
    int top,
    CancellationToken ct)
        {
            using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(ct);

            var langParam = string.IsNullOrWhiteSpace(language) || language.Length < 2
                ? "en"
                : language[..2].ToLower();

    
            var limit = top is > 0 and <= 100 ? top : 10;

            var sql = @"
        WITH auction_check AS (
            SELECT id, seller_user_id
            FROM auctions
            WHERE id = @AuctionId AND org_id = @OrgId AND availability_id = 1
        )
        SELECT 
            ac.id AS ""AuctionId"",
            b.id AS ""BidId"",
        CASE 
            WHEN @Role = 'Admin' THEN b.amount
            WHEN @Role = 'Seller' AND ac.seller_user_id = @CurrentUserId THEN b.amount
            WHEN b.bidder_user_id = @CurrentUserId THEN b.amount
            ELSE NULL 
        END AS ""Amount"",
            b.placed_utc AS ""PlacedUtc"",
            COALESCE(t.name, en_t.name) AS ""BidStateName"",
    
            CASE 
                WHEN @Role = 'Admin' THEN u.email
                WHEN @Role = 'Seller' AND ac.seller_user_id = @CurrentUserId THEN u.email
                WHEN b.bidder_user_id = @CurrentUserId THEN u.email
                ELSE  NULL
            END AS ""BidderEmail"",

            CASE 
                WHEN @Role = 'Admin' THEN b.bidder_user_id
                WHEN @Role = 'Seller' AND ac.seller_user_id = @CurrentUserId THEN b.bidder_user_id
                WHEN b.bidder_user_id = @CurrentUserId THEN b.bidder_user_id
                ELSE NULL
            END AS ""BidderUserId""

        FROM auction_check ac
        LEFT JOIN bids b ON b.auction_id = ac.id AND b.availability_id = 1 AND b.state_id != @RejectedStateId
        LEFT JOIN users u ON u.id = b.bidder_user_id AND u.availability_id = 1
        LEFT JOIN bid_states t ON b.state_id = t.state_id AND t.language = @Language
        LEFT JOIN bid_states en_t ON b.state_id = en_t.state_id AND en_t.language = 'en'
        
        ORDER BY b.amount DESC, b.placed_utc ASC
        LIMIT @Top;
    ";

            var dynParams = new DynamicParameters();
            dynParams.Add("AuctionId", auctionId);
            dynParams.Add("OrgId", orgId);
            dynParams.Add("CurrentUserId", currentUserId);
            dynParams.Add("Role", currentUserRole);
            dynParams.Add("Language", langParam);
            dynParams.Add("Top", limit);
            dynParams.Add("RejectedStateId", (int)BidState.Rejected);

            var items = (await connection.QueryAsync<dynamic>(
                new CommandDefinition(sql, dynParams, cancellationToken: ct)
            )).AsList();


            if (!items.Any())
                return null;

            if (items.Count == 1 && items[0].BidId == null)
            {
                return new LeaderboardDto
                {
                    AuctionId = auctionId,
                    Bids = new List<LeaderboardBidDto>()
                };
            }

            var leaderboard = new LeaderboardDto
            {
                AuctionId = auctionId,
                Bids = items.Select(row => new LeaderboardBidDto
                {
                    BidId = row.BidId,
                    Amount = row.Amount,
                    PlacedUtc = row.PlacedUtc,
                    BidStateName = row.BidStateName,
                    BidderEmail = row.BidderEmail,
                    BidderUserId = row.BidderUserId
                }).ToList()
            };

            return leaderboard;
        }

    }
}
