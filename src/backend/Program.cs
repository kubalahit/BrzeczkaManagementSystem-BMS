using backend;
using InfluxDB.Client;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using backend.Data;
using backend.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// --- Rejestracja Usług ---
builder.Services.AddControllers(); // <-- DODANA KLUCZOWA LINIA
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<BmsDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("PostgresConnection");
    options.UseNpgsql(connectionString);
});

// Rejestracja klienta MQTT jako Singleton
builder.Services.AddSingleton<IManagedMqttClient>(serviceProvider =>
{
    var options = new ManagedMqttClientOptionsBuilder()
        .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
        .WithClientOptions(new MqttClientOptionsBuilder()
            .WithClientId("bms-fake-sensor-01")
            .WithTcpServer("bms-mqtt-broker")
            .Build())
        .Build();

    var mqttClient = new MqttFactory().CreateManagedMqttClient();
    mqttClient.StartAsync(options).GetAwaiter().GetResult();
    return mqttClient;
});

// Rejestracja klienta InfluxDB jako Singleton
builder.Services.AddSingleton<InfluxDBClient>(serviceProvider =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var influxUrl = configuration["INFLUXDB_URL"];
    var influxToken = configuration["INFLUXDB_TOKEN"];

    if (string.IsNullOrEmpty(influxUrl) || string.IsNullOrEmpty(influxToken))
    {
        throw new InvalidOperationException("InfluxDB URL or Token is not configured.");
    }

    return new InfluxDBClient(influxUrl, influxToken);
});

// Rejestracja serwisów działających w tle
builder.Services.AddHostedService<FakeSensorService>();
builder.Services.AddHostedService<MqttIngestionService>();


// --- Konfiguracja Aplikacji ---
var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers(); // <-- DODANA KLUCZOWA LINIA

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<BmsDbContext>();
    // Sprawdzamy, czy w bazie jest już jakakolwiek komora
    if (!context.Chambers.Any())
    {
        // Jeśli nie, tworzymy pierwszą
        context.Chambers.Add(new backend.Models.Chamber { Id = 1, Name = "Komora 01", TargetTemperature = 19.0, Hysteresis = 0.5 });
        context.SaveChanges();
    }
}

app.Run();