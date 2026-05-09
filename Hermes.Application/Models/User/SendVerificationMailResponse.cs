namespace Hermes.Application.Models.User;

/// <summary>Body for successful <c>POST /api/v1/users/{id}/verify</c> (verification mail queued).</summary>
public sealed record SendVerificationMailResponse(int UserId, string Email);
