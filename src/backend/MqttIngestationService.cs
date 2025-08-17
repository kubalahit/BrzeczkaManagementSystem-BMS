// Plik: src/backend/MqttIngestionService.cs (poprawiona wersja)

using System.Text;
using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;

namespace backend;

public class MqttIngestionService : BackgroundService
{
    private readonly ILogger<MqttIngestionService> _logger;
    private readonly IManagedMqttClient _mqttClient;
    private readonly InfluxDBClient _influxClient;
    private readonly string? _influxOrg;
    private readonly string? _influxBucket;

    public MqttIngestionService(ILogger<MqttIngestionService> logger, IManagedMqttClient mqttClient, IConfiguration configuration)
    {
        _logger = logger;
        _mqttClient = mqttClient;

        var influxUrl = configuration["INFLUXDB_URL"];
        var influxToken = configuration["INFLUXDB_TOKEN"];

        _influxOrg = configuration["INFLUXDB_ORG"];
        _influxBucket = configuration["INFLUXDB_BUCKET"];

        _influxClient = new InfluxDBClient(influxUrl, influxToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _mqttClient.ApplicationMessageReceivedAsync += HandleReceivedMessage;

        await _mqttClient.SubscribeAsync("bms/telemetry/+/temperature");

        _logger.LogInformation("MqttIngestionService started and subscribed to topics.");

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
        if (topicParts.Length != 4 || !double.TryParse(payload, out var temperature))
        {
            _logger.LogWarning("Invalid message format. Topic: {Topic}, Payload: {Payload}", topic, payload);
            return Task.CompletedTask;
        }

        var chamberId = topicParts[2];

        WriteToInfluxDb(chamberId, temperature);

        return Task.CompletedTask;
    }

    private void WriteToInfluxDb(string chamberId, double temperature)
    {
        // Poprawka: Sprawdzamy czy konfiguracja została wczytana
        if (string.IsNullOrEmpty(_influxOrg) || string.IsNullOrEmpty(_influxBucket))
        {
            _logger.LogError("InfluxDB organization or bucket not configured. Skipping write.");
            return;
        }

        using var writeApi = _influxClient.GetWriteApi();

        var point = PointData
            .Measurement("temperature")
            .Tag("chamber_id", chamberId)
            .Field("value", temperature)
            .Timestamp(DateTime.UtcNow, WritePrecision.Ns);

        writeApi.WritePoint(point, _influxBucket, _influxOrg);

        _logger.LogInformation("Successfully wrote temperature {Temperature}°C for {ChamberId} to InfluxDB.", temperature, chamberId);
    }
}