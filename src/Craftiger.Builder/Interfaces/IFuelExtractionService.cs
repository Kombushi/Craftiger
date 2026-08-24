using Craftiger.Builder.Models;

namespace Craftiger.Builder.Interfaces;

public interface IFuelExtractionService
{
    FuelData Run(Dump dump, UnifiedItems unified);
}
