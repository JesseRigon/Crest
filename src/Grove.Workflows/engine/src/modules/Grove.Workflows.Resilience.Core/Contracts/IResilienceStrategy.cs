using Polly;

namespace Grove.Workflows.Resilience;

public interface IResilienceStrategy
{
    string Id { get; set; }
    string DisplayName { get; set; }
    Task ConfigurePipeline<T>(ResiliencePipelineBuilder<T> pipelineBuilder, ResilienceContext context);
}