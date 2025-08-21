// Plik: src/firmware/src/main.cpp (wersja finalna z kontrolą)
#include <Arduino.h>
#include "sensor.h"
#include "network.h"

// Definicja pinu, do którego podłączony jest przekaźnik
const int RELAY_PIN = 5;

void setup()
{
  Serial.begin(115200);
  Serial.println("BMS Firmware Starting...");

  pinMode(RELAY_PIN, OUTPUT);
  digitalWrite(RELAY_PIN, LOW); // Upewniamy się, że chłodzenie jest wyłączone na starcie

  setupSensor();
  setupNetwork();
}

void loop()
{
  loopNetwork(); // Pozwól klientowi MQTT działać w tle

  float currentTemperature = readTemperature();

  if (currentTemperature != -127.0)
  {
    Serial.printf("Current: %.2f C, Target: %.2f C\n", currentTemperature, targetTemperature);
    publishTemperature(currentTemperature);

    // --- Logika Histerezy ---
    if (currentTemperature > (targetTemperature + hysteresis))
    {
      digitalWrite(RELAY_PIN, HIGH); // Włącz chłodzenie
      Serial.println("Cooler -> ON");
    }
    else if (currentTemperature < (targetTemperature - hysteresis))
    {
      digitalWrite(RELAY_PIN, LOW); // Wyłącz chłodzenie
      Serial.println("Cooler -> OFF");
    }
  }

  delay(30000);
}