// Plik: src/backend/Controllers/ChamberController.cs
using backend.Data;
using backend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MQTTnet;
using MQTTnet.Extensions.ManagedClient;

namespace backend.Controllers;

// Definiujemy prosty obiekt do przyjmowania danych w zapytaniu POST
public class UpdateSettingsRequest
{
    public double TargetTemperature { get; set; }
    public double Hysteresis { get; set; }
}

[ApiController]
[Route("api/[controller]")] // Dostępny pod adresem /api/chamber
public class ChamberController : ControllerBase
{
    private readonly BmsDbContext _context;
    private readonly ILogger<ChamberController> _logger;
    private readonly IManagedMqttClient _mqttClient;

    // Wstrzykujemy nasz kontekst bazy danych, aby móc z niej korzystać
    public ChamberController(BmsDbContext context, ILogger<ChamberController> logger, IManagedMqttClient mqttClient)
    {
        _context = context;
        _logger = logger;
        _mqttClient = mqttClient;
    }

    // Endpoint do pobierania ustawień konkretnej komory
    // GET /api/chamber/1
    [HttpGet("{id}")]
    public async Task<ActionResult<Chamber>> GetChamberSettings(int id)
    {
        var chamber = await _context.Chambers.FindAsync(id);

        if (chamber == null)
        {
            _logger.LogWarning("Chamber with ID {Id} not found.", id);
            return NotFound(); // Zwraca błąd 404, jeśli komora nie istnieje
        }

        return Ok(chamber); // Zwraca dane komory i status 200 OK
    }

    // Endpoint do aktualizacji ustawień komory
    // POST /api/chamber/1/settings
    [HttpPost("{id}/settings")]
    public async Task<IActionResult> UpdateChamberSettings(int id, [FromBody] UpdateSettingsRequest request)
    {
        var chamber = await _context.Chambers.FindAsync(id);

        if (chamber == null)
        {
            return NotFound();
        }

        // Aktualizujemy dane w obiekcie
        chamber.TargetTemperature = request.TargetTemperature;
        chamber.Hysteresis = request.Hysteresis;

        // Zapisujemy zmiany w bazie danych
        await _context.SaveChangesAsync();

        var topic = $"bms/control/chamber{id:D2}/setpoint";
        var payload = $"{request.TargetTemperature}:{request.Hysteresis}"; // Prosty format: "19.5:0.5"

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .Build();

        await _mqttClient.EnqueueAsync(message);

        _logger.LogInformation("Updated settings for Chamber ID {Id}: TargetTemp={Target}, Hysteresis={Hysteresis}",
            id, request.TargetTemperature, request.Hysteresis);

        return NoContent(); // Zwraca status 204 NoContent, co oznacza sukces bez zwracania danych
    }
}