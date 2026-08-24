using System.Text.Json.Serialization;
using HugoMatters.ApiService.Endpoints;
using HugoMatters.ApiService.Security;
using HugoMatters.Core.Json;
using HugoMatters.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

builder.Services.Configure<InternalApiOptions>(
    builder.Configuration.GetSection(InternalApiOptions.SectionName));

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

app.UseMiddleware<InternalApiAuthenticationMiddleware>();

app.MapGet("/", () => Results.Ok(new { status = "Hugo Matters API is running." }));

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
