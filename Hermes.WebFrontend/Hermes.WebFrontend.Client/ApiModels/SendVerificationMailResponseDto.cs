namespace Hermes.WebFrontend.Client.ApiModels;

/// <summary>
/// Response returned upon successful dispatch of an email verification code.
/// </summary>
/// <param name="UserId">The numeric ID of the user.</param>
/// <param name="Email">The email address to which the verification message was sent.</param>
public sealed record SendVerificationMailResponseDto(int UserId, string Email);
