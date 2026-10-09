using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Crest.Application.Pages;

public sealed class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddPlatformCms();
    }

    public void Configure(IApplicationBuilder app, IHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseStaticFiles();

        app.UsePlatform();
    }
}
