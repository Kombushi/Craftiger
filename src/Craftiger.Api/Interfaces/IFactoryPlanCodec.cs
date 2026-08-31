using Craftiger.Solver.Models.Factory;

namespace Craftiger.Api.Interfaces;

/// <summary>Serializes factory plans for the store; a payload from another artifact build or in any other format decodes to null.</summary>
public interface IFactoryPlanCodec
{
    byte[] Encode(FactoryPlan plan);

    FactoryPlan? Decode(byte[] payload);
}
