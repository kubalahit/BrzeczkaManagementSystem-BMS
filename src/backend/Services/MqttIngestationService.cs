// Plik: src/backend/Services/MqttIngestionService.cs (wersja finalna)
using System.Text;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;

namespace backend.Services;

public class MqttIngestionService : BackgroundService
{
    private readonly ILogger<MqttIngestionService> _logger;
    private readonly IManagedMqttClient _mqttClient;
    private readonly InfluxDBClient _influxClient;
    private readonly string? _influxOrg;
    private readonly string? _influxBucket;

    public MqttIngestionService(ILogger<MqttIngestionService> logger, IManagedMqttClient mqttClient, InfluxDBClient influxClient, IConfiguration configuration)
    {
        _logger = logger;
        _mqttClient = mqttClient;
        _influxClient = influxClient;
        _influxOrg = configuration["INFLUXDB_ORG"];
        _influxBucket = configuration["INFLUXDB_BUCKET"];
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _mqttClient.ApplicationMessageReceivedAsync += HandleReceivedMessage;

        // Subskrybujemy oba tematy telemetryczne za pomocą jednego wildcarda
        await _mqttClient.SubscribeAsync("bms/telemetry/+/+");

        _logger.LogInformation("MqttIngestionService started and subscribed to telemetry topics.");

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
    }

    private Task HandleReceivedMessage(MqttApplicationMessageReceivedEventArgs e)
    {
        var topic = e.ApplicationMessage.Topic;
        var payload = Encoding.UTF8.GetString(e.ApplicationMessage.PayloadSegment);

        var topicParts = topic.Split('/');
        if (topicParts.Length != 4) return Task.CompletedTask; // Ignorujemy niepoprawne tematy

        var chamberId = topicParts[2];
        var measurementType = topicParts[3];

        PointData? point = null;

        // Rozróżniamy, jaki typ wiadomości otrzymaliśmy
        switch (measurementType)
        {
            case "temperature":
                if (double.TryParse(payload, out var temperature))
                {
                    point = PointData.Measurement("temperature")
                                     .Field("value", temperature);
                }
                break;
            case "cooler_state":
                // Zapisujemy ON jako 1, OFF jako 0. Ułatwi to analizę.
                int state = payload.Equals("ON", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                point = PointData.Measurement("cooler_state")
                                 .Field("value", state);
                break;
        }

        // Jeśli udało się stworzyć punkt danych, zapisujemy go do bazy
        if (point != null)
        {
            point.Tag("chamber_id", chamberId)
                 .Timestamp(DateTime.UtcNow, WritePrecision.Ns);

            WriteToInfluxDb(point);
            _logger.LogInformation("Received and stored: {Topic} -> {Payload}", topic, payload);
        }

        return Task.CompletedTask;
    }

    private void WriteToInfluxDb(PointData point)
    {
        if (string.IsNullOrEmpty(_influxOrg) || string.IsNullOrEmpty(_influxBucket))
        {
            _logger.LogError("InfluxDB not configured. Skipping write.");
            return;
        }
        _influxClient.GetWriteApi().WritePoint(point, _influxBucket, _influxOrg);
    }
}