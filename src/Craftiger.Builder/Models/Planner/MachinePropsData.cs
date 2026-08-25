namespace Craftiger.Builder.Models.Planner;

public sealed record MachinePropsData(
    IReadOnlyList<PlannerMachineItem> MachineItems,
    IReadOnlyList<PlannerMachineProps> Props,
    IReadOnlyList<PlannerMachineBonus> Bonuses,
    IReadOnlyList<PlannerTurbineRotor> Rotors,
    IReadOnlyList<PlannerRotorFuelStats> RotorStats);
