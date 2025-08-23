// Plik: src/backend/Services/HostIpService.cs
using System.Net;
using System.Net.Sockets;

namespace backend.Services;

public class HostIpService
{
    public string? HostIpAddress { get; private set; }

    public HostIpService()
    {
        // Ta logika znajduje pierwszy, sensowny (nie-wewnętrzny) adres IPv4 komputera
        var host = Dns.GetHostEntry(Dns.GetHostName());
        HostIpAddress = host.AddressList.FirstOrDefault(ip =>
            ip.AddressFamily == AddressFamily.InterNetwork &&
            !IPAddress.IsLoopback(ip))?.ToString();
    }
}