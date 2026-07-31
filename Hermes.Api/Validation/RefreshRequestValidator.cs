using FluentValidation;
using Hermes.Application.DTOs.Login;

namespace Hermes.Api.Validation;

public sealed class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator()
    {
        RuleFor(refreshRequest => refreshRequest.RefreshToken).NotEmpty();
    }
}
