/// <summary>Serilog bootstrap logger freezes after first host build — disable parallel test collections to avoid multiple in-process factories.</summary>
[assembly: CollectionBehavior(DisableTestParallelization = true)]
