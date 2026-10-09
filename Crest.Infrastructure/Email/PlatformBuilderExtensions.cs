using Crest.Email;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Provides an extension method for <see cref="PlatformBuilder"/>.
/// </summary>
public static partial class PlatformBuilderExtensions
{
    /// <summary>
    /// Adds e-mail address validator service.
    /// </summary>
    /// <param name="builder">The <see cref="PlatformBuilder"/>.</param>
    public static PlatformBuilder AddEmailAddressValidator(this PlatformBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.AddTransient<IEmailAddressValidator, EmailAddressValidator>();
        });

        return builder;
    }
}
