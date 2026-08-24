namespace Craftiger.Builder.Models;

/// <summary>A large boiler controller's EU/t rating, the basis of its steam rate.</summary>
public sealed record DumpBoiler(string ItemId, int EuT);
