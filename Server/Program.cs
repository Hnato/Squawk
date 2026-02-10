using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using System;
using System.IO;

using Squawk.Game;

var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args,
    WebRootPath = "Client" 
});

builder.Services.AddRazorPages();
builder.Services.AddControllers();
builder.Services.AddSingleton<GameWorld>();
builder.Services.AddHostedService<GameEngine>();
builder.Services.AddSingleton<GameEngine>(sp => (GameEngine)sp.GetRequiredService<IHostedService>()); // Allow injection of GameEngine

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error");
}

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();
app.MapControllers();
app.MapFallbackToFile("index.html");
app.Urls.Add("http://0.0.0.0:5005");
app.Run();
