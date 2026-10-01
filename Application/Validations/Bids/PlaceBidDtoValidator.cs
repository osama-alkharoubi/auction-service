using Application.Dto.Bids;
using FluentValidation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Validations.Bids
{
    public class PlaceBidDtoValidator : AbstractValidator<PlaceBidDto>
    {
        public PlaceBidDtoValidator()
        {
            RuleFor(x => x.AuctionId)
                .NotEmpty().WithMessage("AuctionId is required.");

            RuleFor(x => x.BidderUserId)
                .NotEmpty().WithMessage("BidderUserId is required.");

            RuleFor(x => x.Amount)
                .GreaterThan(0).WithMessage("Bid amount must be greater than zero.")
                .Must(amount => decimal.Round(amount, 2) == amount)
                .WithMessage("Bid amount cannot have more than two decimal places.");

            RuleFor(x => x.IdempotencyKey)
                .NotEmpty().WithMessage("IdempotencyKey is required.");
        }
    }
}

