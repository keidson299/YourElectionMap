using YourElectionMap.Server.Districts;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();

// Every state's boundaries, megabytes of them, read from disk once and shared by every lookup.
builder.Services.AddSingleton<DistrictData>();

// A typed client gets a pooled, rotated handler but is itself transient, so the
// lookup service that holds one is scoped rather than a singleton.
builder.Services.AddHttpClient<CensusGeocoder>();
builder.Services.AddHttpClient<GoogleVoterInfo>();
builder.Services.AddScoped<DistrictLookupService>();

// Boundaries go out at full detail, tens of kilobytes of coordinates for a lookup and
// a few hundred for the largest, which compress to about a third. Safe over HTTPS
// here: what makes that risky is a secret in the same response as text an attacker
// chose, and these hold no secrets.
builder.Services.AddResponseCompression(options => options.EnableForHttps = true);

var app = builder.Build();

app.UseResponseCompression();

// Read the boundaries now, so a missing or broken data file stops the app at start
// rather than failing every lookup, and the first visitor does not wait on the parse.
app.Services.GetRequiredService<DistrictData>();

app.UseDefaultFiles();
app.MapStaticAssets();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapControllers();

app.MapFallbackToFile("/index.html");

app.Run();

// Exposed so the test project can host the app in memory with WebApplicationFactory<Program>.
public partial class Program { }
