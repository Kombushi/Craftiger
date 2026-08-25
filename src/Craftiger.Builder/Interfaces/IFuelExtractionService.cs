using Craftiger.Builder.Models.Dump;
using Craftiger.Builder.Models.Planner;

namespace Craftiger.Builder.Interfaces;

/// <summary>Reads what each fuel-flagged recipe map burns, per its configured family.</summary>
public interface IFuelExtractionService
{
    FuelData Run(Dump dump, UnifiedItems unified);
}
