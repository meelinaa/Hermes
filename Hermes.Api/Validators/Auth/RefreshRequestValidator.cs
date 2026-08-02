using FluentValidation;
using Hermes.Application.DTOs.Login;

namespace Hermes.Api.Validators.Auth;

/// <summary>
/// Validator for the <see cref="RefreshRequestDto"/> to ensure refresh token requests are valid.
/// </summary>
public sealed class RefreshRequestValidator : AbstractValidator<RefreshRequestDto>
{
    /// <summary>
    /// Initializes validation rules ensuring refresh token string is provided.
    /// </summary>
    public RefreshRequestValidator()
    {
        RuleFor(refreshRequest => refreshRequest.RefreshToken).NotEmpty();
    }
}
