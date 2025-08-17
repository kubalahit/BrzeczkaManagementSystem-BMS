// Plik: src/backend/Controllers/TemperatureController.cs

using InfluxDB.Client;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")] // Dostępny pod adresem /api/temperature
public class TemperatureController : ControllerBase
{
    private readonly ILogger<TemperatureController> _logger;
    private readonly InfluxDBClient _influxClient;
    private readonly string? _influxOrg;
    private readonly string? _influxBucket;

    public TemperatureController(ILogger<TemperatureController> logger, IConfiguration configuration, InfluxDBClient influxClient)
    {
        _logger = logger;

        _influxOrg = configuration["INFLUXDB_ORG"];
        _influxBucket = configuration["INFLUXDB_BUCKET"];

        _influxClient = influxClient;
    }

    // Endpoint do pobierania ostatniego odczytu temperatury
    [HttpGet("latest")] // Dostępny pod adresem /api/temperature/latest
    public async Task<IActionResult> GetLatest()
    {
        var queryApi = _influxClient.GetQueryApi();

        var fluxQuery = $@"from(bucket: ""{_influxBucket}"")
                          |> range(start: -1h) 
                          |> filter(fn: (r) => r._measurement == ""temperature"")
                          |> filter(fn: (r) => r._field == ""value"")
                          |> last()";

        var result = await queryApi.QueryAsync(fluxQuery, _influxOrg);

        if (result.Any() && result[0].Records.Any())
        {
            var record = result[0].Records.First();
            return Ok(new
            {
                time = record.GetTime()?.ToDateTimeUtc(), // Poprawne pobranie czasu
                value = Math.Round((double)record.GetValue(), 2) // Zaokrąglenie do 2 miejsc
            });
        }

        return NotFound("No data found.");
    }

    // Endpoint do pobierania historii odczytów
    [HttpGet("history")] // Dostępny pod adresem /api/temperature/history
    public async Task<IActionResult> GetHistory([FromQuery] string range = "1h")
    {
        var queryApi = _influxClient.GetQueryApi();

        var fluxQuery = $@"from(bucket: ""{_influxBucket}"")
                          |> range(start: -{range})
                          |> filter(fn: (r) => r._measurement == ""temperature"")
                          |> filter(fn: (r) => r._field == ""value"")
                          |> aggregateWindow(every: 1m, fn: mean, createEmpty: false)
                          |> yield(name: ""mean"")";

        var result = await queryApi.QueryAsync(fluxQuery, _influxOrg);

        if (result.Any() && result[0].Records.Any())
        {
            var dataPoints = result[0].Records.Select(record => new
            {
                time = record.GetTime()?.ToDateTimeUtc(), // Poprawne pobranie czasu
                value = Math.Round((double)record.GetValue(), 2) // Zaokrąglenie do 2 miejsc
            });
            return Ok(dataPoints);
        }

        return NotFound("No data found.");
    }
}