using FluentValidation;

namespace MinhaEscala.Application;

public sealed record RegisterRequest(string Name, string Email, string Password, string? InvitationToken = null);
public sealed record LoginRequest(string Email, string Password);
public sealed record ForgotRequest(string Email);
public sealed record ResetRequest(string Token, string Password);
public sealed record EntryRequest(DateOnly Date, string Type, string Sector);
public sealed record InviteRequest(string Email);
public sealed record UserDto(Guid Id, Guid TenantId, string Name, string Email, string Role, bool IsActive);
public sealed record EntryDto(Guid Id, DateOnly Date, string Type, string Sector);
public sealed record AuthDto(string AccessToken, DateTimeOffset ExpiresAt, UserDto User);
public sealed record TokenPair(AuthDto Auth, string RefreshToken, DateTimeOffset RefreshExpiresAt);

public sealed class ApiException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}

public sealed class RegisterValidator : AbstractValidator<RegisterRequest>
{
    public RegisterValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(254);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(12).MaximumLength(128);
        RuleFor(x => x.InvitationToken).MaximumLength(128);
    }
}
public sealed class LoginValidator : AbstractValidator<LoginRequest>
{
    public LoginValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(254);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
    }
}
public sealed class ForgotValidator : AbstractValidator<ForgotRequest>
{
    public ForgotValidator() => RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(254);
}
public sealed class ResetValidator : AbstractValidator<ResetRequest>
{
    public ResetValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(128);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(12).MaximumLength(128);
    }
}
public sealed class EntryValidator : AbstractValidator<EntryRequest>
{
    public EntryValidator()
    {
        RuleFor(x => x.Date).InclusiveBetween(new DateOnly(1900, 1, 1), new DateOnly(2100, 12, 31));
        RuleFor(x => x.Type).Must(x => x is "folga" or "trabalho").WithMessage("Escolha folga ou trabalho.");
        RuleFor(x => x.Sector).NotNull().MaximumLength(120);
    }
}
public sealed class InviteValidator : AbstractValidator<InviteRequest>
{
    public InviteValidator() => RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(254);
}
