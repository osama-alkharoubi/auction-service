using Application.Dto.Auctions;
using FluentValidation;
using Domain.Enums;
namespace Application.Validations.Auctions
{
    public class ChangeAuctionStateDtoValidator : AbstractValidator<ChangeStateApiRequest>
    {
        public ChangeAuctionStateDtoValidator()
        {
            RuleFor(x => x.NewState)
                .Must(state => Enum.IsDefined(typeof(AuctionState), state))
                .WithMessage("Invalid auction state provided.");

            RuleFor(x => x.Reason)
                .NotEmpty()
                .When(x => x.NewState == (int)AuctionState.Cancelled)
                .WithMessage("A reason must be provided when cancelling an auction.");
        }
    }
}
