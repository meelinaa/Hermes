/// <summary>
/// Integration tests boot a full <see cref="Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory{TEntryPoint}"/> host per fixture.
/// Hermes.Api wires Serilog with a reloadable/bootstrap logger that must only be frozen once per process; parallel collections would start multiple hosts and trigger
/// <see cref="InvalidOperationException"/> (&quot;The logger is already frozen&quot;).
/// </summary>
[assembly: CollectionBehavior(DisableTestParallelization = true)]
