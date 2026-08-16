namespace Hermes.WebFrontend.Client.ApiModels;

/// <summary>
/// Payload submitted by the user containing the email verification code.
/// </summary>
/// <param name="UserId">The numeric ID of the user verifying their email.</param>
/// <param name="Code">The 6-digit OTP verification code.</param>
public sealed record UserVerificationCodeRequestDto(int UserId, string Code);
