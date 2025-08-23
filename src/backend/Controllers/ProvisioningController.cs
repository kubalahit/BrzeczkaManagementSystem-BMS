// Plik: src/backend/Controllers/ProvisioningController.cs
using System.Text;
using System.Text.Json;
using backend.Data;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace backend.Controllers;

public class ProvisioningRequest
{
    public string IpAddress { get; set; } = string.Empty;
    public string ChamberName { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
}

[ApiController]
[Route("api/[controller]")]
public class ProvisioningController : ControllerBase
{
    private readonly ILogger<ProvisioningController> _logger;
    private readonly HostIpService _hostIpService;
    private readonly BmsDbContext _dbContext;

    public ProvisioningController(ILogger<ProvisioningController> logger, HostIpService hostIpService, BmsDbContext dbContext)
    {
        _logger = logger;
        _hostIpService = hostIpService;
        _dbContext = dbContext;
    }

    // Endpoint do wysyłania konfiguracji do nowego urządzenia
    // POST /api/provisioning/configure
    [HttpPost("configure")]
    public async Task<IActionResult> ConfigureDevice([FromBody] ProvisioningRequest request)
    {
        if (string.IsNullOrEmpty(request.IpAddress) || string.IsNullOrEmpty(request.MacAddress)) // <-- Dodaliśmy MAC
        {
            return BadRequest("IP and MAC address are required.");
        }

        // Sprawdzamy, czy urządzenie już istnieje w bazie
        var chamber = await _dbContext.Chambers
            .FirstOrDefaultAsync(c => c.MacAddress == request.MacAddress);

        if (chamber == null)
        {
            // Jeśli nie, tworzymy nowe
            _logger.LogInformation("Device with MAC {MAC} is new. Creating new chamber entry.", request.MacAddress);
            chamber = new Chamber
            {
                MacAddress = request.MacAddress,
                Name = request.ChamberName, // Używamy nazwy z frontendu
                TargetTemperature = 19.0, // Domyślne wartości
                Hysteresis = 0.5
            };
            _dbContext.Chambers.Add(chamber);
            await _dbContext.SaveChangesAsync(); // Zapisujemy, aby nadać mu ID
        }

        // Przygotowujemy unikalne tematy MQTT
        string chamberIdString = $"chamber{chamber.Id:D2}";

        var configPayload = new
        {
            mqtt_server = _hostIpService.HostIpAddress,
            mqtt_port = 1883,
            client_id = chamberIdString,
            status_topic = $"bms/status/{chamberIdString}",
            temp_topic = $"bms/telemetry/{chamberIdString}/temperature",
            state_topic = $"bms/telemetry/{chamberIdString}/cooler_state",
            setpoint_topic = $"bms/control/{chamberIdString}/setpoint"
        };

        var jsonPayload = JsonSerializer.Serialize(configPayload);
        var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        try
        {
            using var httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            var response = await httpClient.PostAsync($"http://{request.IpAddress}/configure", content);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully configured device at {IpAddress}", request.IpAddress);
                // TODO: Tutaj w przyszłości zapiszemy nową komorę do bazy PostgreSQL
                return Ok(new { message = "Device configured successfully." });
            }

            _logger.LogError("Failed to configure device at {IpAddress}. Status: {StatusCode}", request.IpAddress, response.StatusCode);
            return StatusCode((int)response.StatusCode, "Failed to configure device.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception occurred while trying to configure device at {IpAddress}", request.IpAddress);
            return StatusCode(500, "An exception occurred.");
        }
    }
}