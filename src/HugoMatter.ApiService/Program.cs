using System.Text.Json.Serialization;
using HugoMatter.ApiService.Endpoints;
using HugoMatter.Core.Json;
using HugoMatter.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.SerializerOptions.Converters.Add(new ContentTypeKindJsonConverter());
});

builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

await DependencyInjection.InitializeInfrastructureAsync(app.Services);

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/", () => Results.Ok(new { status = "Hugo Matter API is running." }));

app.MapConnectionEndpoints();
app.MapSessionEndpoints();
app.MapContentEndpoints();
app.MapSiteConfigEndpoints();
app.MapPreviewEndpoints();
app.MapThemePackEndpoints();

app.MapDefaultEndpoints();

app.Run();

/// <summary>
/// Entry point marker for WebApplicationFactory.
/// </summary>
public partial class Program;
