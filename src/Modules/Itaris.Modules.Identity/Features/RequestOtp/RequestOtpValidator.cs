using System.Text.RegularExpressions;
using FluentValidation;

namespace Itaris.Modules.Identity.Features.RequestOtp;

/// <summary>doc 05 A1 validation: E.164, +962 only.</summary>
public sealed partial class RequestOtpValidator : AbstractValidator<RequestOtpRequest>
{
    [GeneratedRegex(@"^\+962\d{8,9}$")]
    private static partial Regex JordanE164();

    public RequestOtpValidator()
    {
        RuleFor(r => r.PhoneNumber)
            .NotEmpty()
            .Matches(JordanE164())
            .WithMessage("Phone number must be E.164 format with +962 country code.");

        RuleFor(r => r.Purpose)
            .NotEmpty()
            .Equal("login");
    }
}
