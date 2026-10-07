using YourElectionMap.Server.Districts;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

// Four megabytes of boundaries, read from disk once and shared by every lookup.
builder.Services.AddSingleton<DistrictData>();

// Typed client, so its handler is pooled and rotated rather than held for the life of
// the process. That makes it per-request, and the service that uses it too.
builder.Services.AddHttpClient<CensusGeocoder>();
builder.Services.AddScoped<DistrictLookupService>();

var app = builder.Build();

// Read the boundaries now, so a missing or broken data file stops the app at start
// rather than failing every lookup, and the first visitor does not wait on the parse.
app.Services.GetRequiredService<DistrictData>();

app.UseDefaultFiles();
app.MapStaticAssets();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.MapFallbackToFile("/index.html");

app.Run();

// Exposed so the test project can host the app in memory with WebApplicationFactory<Program>.
public partial class Program { }
