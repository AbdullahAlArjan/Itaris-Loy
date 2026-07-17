using Itaris.Modules.Identity.Features.RequestOtp;

namespace Itaris.Tests.Unit.Identity;

public class RequestOtpValidatorTests
{
    private readonly RequestOtpValidator _validator = new();

    [Theory]
    [InlineData("+962790000001")]
    [InlineData("+96279000000")]
    public void Accepts_jordanian_e164_numbers(string phone)
    {
        var result = _validator.Validate(new RequestOtpRequest(phone, "login"));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0790000001")]
    [InlineData("+15551234567")]
    [InlineData("+962-79-000-0001")]
    public void Rejects_non_jordanian_or_malformed_numbers(string phone)
    {
        var result = _validator.Validate(new RequestOtpRequest(phone, "login"));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Rejects_unknown_purpose()
    {
        var result = _validator.Validate(new RequestOtpRequest("+962790000001", "hack"));

        Assert.False(result.IsValid);
    }
}
