namespace Craftiger.Solver.Models.Factory;

/// <summary>A dynamo hatch's verdict on a line: the net EU/t it emits after capping and the Enet loss, at its voltage tier.</summary>
public readonly record struct HatchChoice(double NetEuT, int Tier);
