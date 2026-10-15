namespace Grove.Integrations.AI.Azure.Models;

public sealed class VectorSearchProfileMap
{
    public string Name { get; set; }

    public string AlgorithmConfigurationName { get; set; }

    public string VectorizerName { get; set; }

    public string CompressionName { get; set; }
}
