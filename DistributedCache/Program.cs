using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Distributed;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddStackExchangeRedisCache(options =>
 {
     options.Configuration = builder.Configuration.GetConnectionString("Redis");
 });

builder.Services.AddDistributedMemoryCache();
builder.Services.AddHttpClient();

var app = builder.Build();

app.MapGet("/api/weather", async (
    [FromQuery] string city,
    [FromServices] IDistributedCache cache,
    [FromServices] IHttpClientFactory httpClientFactory) =>
{
    var key = $"weather_{city}";
    var stopwatch = Stopwatch.StartNew();

    var cachedData = await cache.GetStringAsync(key);
    if (cachedData != null)
    {
        var cachedResult = JsonSerializer.Deserialize<object>(cachedData);
        stopwatch.Stop();
        return Results.Ok(new
        {
            source = "cache",
            timeMs = stopwatch.ElapsedMilliseconds,
            data = cachedResult
        });
    }

    using var client = httpClientFactory.CreateClient();
    var result = await GetWeather(client, city);

    var serializedData = JsonSerializer.Serialize(result);
    await cache.SetStringAsync(key, serializedData, new DistributedCacheEntryOptions
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
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

    HttpResponseMessage response = await client.GetAsync(apiUrl);
    if (!response.IsSuccessStatusCode)
    {
        return null;
    }

    return await response.Content.ReadFromJsonAsync<object>();
}
