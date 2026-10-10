using Microsoft.Extensions.DependencyInjection;
using Crest.Workflows.Platform.Activities;
using Crest.Workflows.Platform.Options;

namespace Crest.Workflows.Platform.Helpers;

public static class ServiceCollectionExtensions
{
    /// <summary>Adds an activity to the platform activity library Crest.Workflows runs.</summary>
    public static IServiceCollection AddActivity<TActivity>(this IServiceCollection services)
        where TActivity : class, IActivity
    {
        services.Configure<WorkflowOptions>(options => options.RegisterActivityType<TActivity>());

        return services;
    }
}
