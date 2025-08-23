// Plik: src/backend/Services/DeviceDiscoveryService.cs (wersja odporna na duplikaty)
using Makaretu.Dns;
using System.Collections.Concurrent;

namespace backend.Services;

public class DeviceDiscoveryService : BackgroundService
{
    private readonly ILogger<DeviceDiscoveryService> _logger;
    private readonly ServiceDiscovery _serviceDiscovery;

    // Nasza pamięć podręczna: przechowuje MAC adres i czas, kiedy go ostatnio widzieliśmy
    private readonly ConcurrentDictionary<string, DateTime> _recentlyDiscoveredDevices = new();

    public DeviceDiscoveryService(ILogger<DeviceDiscoveryService> logger)
    {
        _logger = logger;
        _serviceDiscovery = new ServiceDiscovery();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Device Discovery Service is starting.");

        _serviceDiscovery.ServiceInstanceDiscovered += (s, e) =>
        {
            var fullServiceName = e.ServiceInstanceName.ToString();
            _logger.LogInformation(">>> Znaleziono coś <<<");
            if (fullServiceName.Contains("_bms-chamber._tcp"))
            {
                _logger.LogInformation(">>> Znaleziono _bms-chamber <<<");
                var instanceName = e.ServiceInstanceName.Labels.FirstOrDefault() ?? string.Empty;
                var macAddress = instanceName.Split('-').LastOrDefault();
                var aRecord = e.Message.Answers.FirstOrDefault(a => a.Type == DnsType.A) as ARecord;
                if (aRecord is null) aRecord = e.Message.AdditionalRecords.FirstOrDefault(a => a.Type == DnsType.A) as ARecord;
                var ipAddress = aRecord?.Address.ToString();

                // Sprawdzamy, czy mamy wszystkie potrzebne dane
                if (!string.IsNullOrEmpty(macAddress) && !string.IsNullOrEmpty(ipAddress))
                {
                    // --- NOWA, KLUCZOWA LOGIKA ---
                    // Sprawdzamy, czy widzieliśmy to urządzenie w ciągu ostatnich 30 sekund
                    if (_recentlyDiscoveredDevices.TryGetValue(macAddress, out var lastSeen) &&
                        (DateTime.UtcNow - lastSeen).TotalSeconds < 30)
                    {
                        // Jeśli tak, ignorujemy ten duplikat
                        return;
                    }

                    // Jeśli nie, przetwarzamy je i zapisujemy w naszej pamięci podręcznej
                    _recentlyDiscoveredDevices[macAddress] = DateTime.UtcNow;
                    // --- KONIEC NOWEJ LOGIKI ---

                    _logger.LogInformation(">>> New Unconfigured BMS Chamber Discovered! <<<");
                    _logger.LogInformation("   MAC Address: {MAC}", macAddress);
                    _logger.LogInformation("   IP Address: {IP}", ipAddress);
                }
            }
        };

        _serviceDiscovery.QueryServiceInstances("_bms-chamber._tcp");

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
    }
}