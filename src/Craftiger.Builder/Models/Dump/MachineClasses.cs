namespace Craftiger.Builder.Models.Dump;

/// <summary>Java class-name suffixes identifying machine kinds in the dump's class export.</summary>
public static class MachineClasses
{
    public const string TreeFarm = ".MTETreeFarm";
    public const string XlTurbines = ".xlturbines.";
    public const string SteamTurbine = ".MTESteamTurbine";
    private const string LargeBoiler = ".MTELargeBoiler";

    /// <summary>The generation suffix of a large boiler's class ("Bronze" … "TungstenSteel"); null for other classes.</summary>
    public static string? BoilerGenerationOf(string machineClass)
    {
        var index = machineClass.LastIndexOf(LargeBoiler, StringComparison.Ordinal);
        if (index < 0)
        {
            return null;
        }
        var generation = machineClass[(index + LargeBoiler.Length)..];
        return generation.Length > 0 ? generation : null;
    }

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
