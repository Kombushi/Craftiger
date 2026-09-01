namespace Craftiger.Builder.Models.Dump;

/// <summary>Java class-name suffixes identifying machine kinds in the dump's class export.</summary>
public static class MachineClasses
{
    public const string TreeFarm = ".MTETreeFarm";
    public const string XlTurbines = ".xlturbines.";

    /// <summary>The rotor stat class a turbine controller spins; HP and SC steam kinds burn fuels the model does not rate.</summary>
    public static string? RotorFuelOf(string machineClass)
    {
        if (EndsWithKind(machineClass, "TurbineSteam"))
        {
            return "STEAM";
        }
        if (EndsWithKind(machineClass, "TurbineGas"))
        {
            return "GAS";
        }
        return EndsWithKind(machineClass, "TurbinePlasma") ? "PLASMA" : null;
    }

    private static bool EndsWithKind(string machineClass, string kind) =>
        machineClass.EndsWith($".MTELarge{kind}", StringComparison.Ordinal)
        || machineClass.EndsWith($".MTEXL{kind}", StringComparison.Ordinal);
}
