using YourElectionMap.Server.Districts;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSingleton<DistrictData>();
builder.Services.AddHttpClient<CensusGeocoder>();
builder.Services.AddHttpClient<GoogleVoterInfo>();
builder.Services.AddMemoryCache();

// Full-detail boundaries compress to about a third. Safe over HTTPS here: the risk
// needs a secret in the same response as attacker-chosen text, and these hold none.
builder.Services.AddResponseCompression(options => options.EnableForHttps = true);

var app = builder.Build();

app.UseResponseCompression();

// Load the boundaries now, so a missing or broken data file stops the app at start
// and the first visitor does not wait on the parse.
app.Services.GetRequiredService<DistrictData>();

app.UseDefaultFiles();
app.MapStaticAssets();
app.UseHttpsRedirection();
app.MapControllers();
app.MapFallbackToFile("/index.html");

app.Run();

// For WebApplicationFactory<Program> in the tests.
public partial class Program { }
