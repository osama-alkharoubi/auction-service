using Application.Dto.Auctions;
using FluentValidation;
using System.Globalization;

namespace Application.Validations.Auctions
{
    public class AuctionQueryParametersValidator : AbstractValidator<AuctionQueryParameters>
    {
        public AuctionQueryParametersValidator()
        {
            RuleFor(parameters => parameters.PageSize)
                .InclusiveBetween(1, 100)
                .WithMessage("PageSize must be between 1 and 100.");

            RuleFor(parameters => parameters.State)
                .Must(state => !state.HasValue || Enum.IsDefined(state.Value))
                .WithMessage("State must be a valid auction state.");

            RuleFor(parameters => parameters.Search)
                .MaximumLength(200)
                .When(parameters => parameters.Search is not null)
                .WithMessage("Search cannot exceed 200 characters.");

            RuleFor(parameters => parameters.Cursor)
                .Must(IsValidCursor)
                .When(parameters => !string.IsNullOrWhiteSpace(parameters.Cursor))
                .WithMessage("Cursor must use the CreatedUtc_Id format with an ISO 8601 timestamp and a GUID.");
        }

        private static bool IsValidCursor(string? cursor)
        {
            if (string.IsNullOrWhiteSpace(cursor))
                return true;

            var separatorIndex = cursor.LastIndexOf('_');
            if (separatorIndex <= 0 || separatorIndex == cursor.Length - 1)
                return false;

            var timestamp = cursor[..separatorIndex];
            var id = cursor[(separatorIndex + 1)..];

            return DateTimeOffset.TryParse(
                       timestamp,
                       CultureInfo.InvariantCulture,
                       DateTimeStyles.None,
                       out _) &&
                   Guid.TryParse(id, out _);
        }
    }
}
