using Application.Dto.Auth;
using FluentValidation;
using FluentValidation.Validators;


namespace Application.Validations
{
    public class LoginRequestDtoValidator : AbstractValidator<LoginRequestDto>
    {
        public LoginRequestDtoValidator()
        {
            RuleFor(x => x.Email)
             .NotEmpty().WithMessage("Email is required.")
             .MaximumLength(255).WithMessage("Email must not exceed 255 characters.")
             .EmailAddress(EmailValidationMode.AspNetCoreCompatible)
             .WithMessage("Email must be a valid email address.");

            RuleFor(x => x.Password)
                .NotEmpty().WithMessage("Password is required.")
                .MinimumLength(6).WithMessage("Password must be at least 6 characters long.")
                .MaximumLength(100).WithMessage("Password must not exceed 100 characters.");
        }
    }
}
