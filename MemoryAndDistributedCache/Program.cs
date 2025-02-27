using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMemoryCache();
builder.Services.AddHttpClient();

var app = builder.Build();

app.MapGet("/api/weather", async (
    [FromQuery] string city,
    [FromServices] IMemoryCache cache,
    [FromServices] IHttpClientFactory httpClientFactory) =>
{
    var key = $"weather_{city}";
    var stopwatch = Stopwatch.StartNew();

    bool isCacheHit = cache.TryGetValue(key, out object? cachedResult);

    if (isCacheHit && cachedResult != null)
    {
        stopwatch.Stop();
        return Results.Ok(new
        {
            source = "cache",
            timeMs = stopwatch.ElapsedMilliseconds,
            data = cachedResult
        });
    }

    var result = await cache.GetOrCreateAsync(key, async (entry) =>
    {
        entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
        using var client = httpClientFactory.CreateClient();
        return await GetWeather(client, city);
    });

    stopwatch.Stop();
    return Results.Ok(new
    {
        source = "api",
        timeMs = stopwatch.ElapsedMilliseconds,
        data = result
    });
});

app.Run();

async Task<object?> GetWeather(HttpClient client, string city)
{
    string apiKey = "11c054840cc524cc27ec08f273bd09c9";
    string apiUrl = $"https://api.openweathermap.org/data/2.5/weather?q={city}&appid={apiKey}&units=metric";

    var response = await client.GetAsync(apiUrl);

    return await response.Content.ReadFromJsonAsync<object>();
}
