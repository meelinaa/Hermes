namespace Hermes.Api;

/// <summary>
/// Marker type for <see cref="Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory{TEntryPoint}"/> in integration tests
/// (avoids ambiguity with <c>Program</c> from referenced worker assembly).
/// </summary>
public sealed class ApiWebApplicationMarker;
