var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddPlatform()
    .AddMvc();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}

app.UseStaticFiles();

app.UsePlatform();

await app.RunAsync();
