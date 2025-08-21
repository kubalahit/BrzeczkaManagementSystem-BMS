// Plik: src/backend/FakeSensorService.cs (nowa wersja)

using MQTTnet;
using MQTTnet.Extensions.ManagedClient;

namespace backend.Services;

public class FakeSensorService : BackgroundService
{
    private readonly ILogger<FakeSensorService> _logger;
    private readonly IManagedMqttClient _mqttClient;

    // Prosimy .NET o wstrzyknięcie gotowego, działającego klienta MQTT
    public FakeSensorService(ILogger<FakeSensorService> logger, IManagedMqttClient mqttClient)
    {
        _logger = logger;
        _mqttClient = mqttClient;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Czekamy chwilę na ustabilizowanie się połączenia MQTT
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
        _logger.LogInformation("Fake Sensor Service is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            // Sprawdzamy, czy klient jest połączony, zanim spróbujemy coś wysłać
            if (_mqttClient.IsConnected)
            {
                var random = new Random();
                double fakeTemperature = Math.Round(18.0 + random.NextDouble() * 5, 2);
                string messagePayload = fakeTemperature.ToString();

                var message = new MqttApplicationMessageBuilder()
                    .WithTopic("bms/telemetry/chamber01/temperature")
                    .WithPayload(messagePayload)
                    .Build();

                await _mqttClient.EnqueueAsync(message);
                _logger.LogInformation("Published fake temperature: {Temperature}°C", fakeTemperature);
            }
            else
            {
                _logger.LogWarning("MQTT client not connected. Skipping publish.");
            }

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
        }

        _logger.LogInformation("Fake Sensor Service is stopping.");
    }
}