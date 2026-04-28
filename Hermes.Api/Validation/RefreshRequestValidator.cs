using FluentValidation;
using Hermes.Application.Models.Login;

namespace Hermes.Api.Validation;

/// <summary>Requires the opaque refresh token string sent to <c>POST /auth/refresh</c>.</summary>
public sealed class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator()
    {
        RuleFor(x => x.RefreshToken).NotEmpty();
    }
}
