using Craftiger.Builder.Models.Dump;
using Craftiger.Builder.Models.Planner;

namespace Craftiger.Builder.Interfaces;

/// <summary>Synthesizes factory-scoped Crop Manager and Industrial Farm lines from the dump's CropsNH tables.</summary>
public interface ICropFarmRecipeService
{
    CropFarms Run(Dump dump, UnifiedItems unified);
}
