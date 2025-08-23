// Plik: src/backend/Models/Chamber.cs
namespace backend.Models;

public class Chamber
{
    public int Id { get; set; } // Klucz główny
    public string Name { get; set; } = string.Empty;
    public double TargetTemperature { get; set; }
    public double Hysteresis { get; set; }
    public string MacAddress { get; set; } = string.Empty;
}