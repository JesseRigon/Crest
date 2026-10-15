using Microsoft.AspNetCore.Builder;
using Grove;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides an extension method for <see cref="PlatformBuilder"/>.
/// </summary>
public static partial class PlatformBuilderExtensions
{
    /// <summary>
    /// Adds phone format validator service.
    /// </summary>
    /// <param name="builder">The <see cref="PlatformBuilder"/>.</param>
    public static PlatformBuilder AddPhoneFormatValidator(this PlatformBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.AddTransient<IPhoneFormatValidator, PhoneFormatValidator>();
        });

        return builder;
    }
}
