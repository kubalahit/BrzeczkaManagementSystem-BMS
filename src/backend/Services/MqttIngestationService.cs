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

        await _mqttClient.SubscribeAsync("bms/telemetry/+/+");
        await _mqttClient.SubscribeAsync("bms/status/+");

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

        // Ignorujemy niepoprawnie sformatowane tematy
        if (topicParts.Length < 3) return Task.CompletedTask;

        var topicType = topicParts[1]; // "telemetry" lub "status"
        var chamberId = topicParts[2]; // np. "chamber01"
        PointData? point = null;

        switch (topicType)
        {
            case "telemetry":
                if (topicParts.Length != 4) return Task.CompletedTask;
                var measurementType = topicParts[3];

                if (measurementType == "temperature" && double.TryParse(payload, out var temp))
                {
                    point = PointData.Measurement("temperature").Field("value", temp);
                }
                else if (measurementType == "cooler_state")
                {
                    int state = payload.Equals("ON", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                    point = PointData.Measurement("cooler_state").Field("value", state);
                }
                break;

            case "status":
                int isOnline = payload.Equals("ONLINE", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
                point = PointData.Measurement("status").Field("is_online", isOnline);
                break;
        }

        if (point != null)
        {
            point.Tag("chamber_id", chamberId).Timestamp(DateTime.UtcNow, WritePrecision.Ns);
            WriteToInfluxDb(point);
            _logger.LogInformation("Stored to InfluxDB: {Topic} -> {Payload}", topic, payload);
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