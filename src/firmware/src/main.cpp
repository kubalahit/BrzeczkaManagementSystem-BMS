// Plik: src/firmware/src/main.cpp (wersja z okresowym raportowaniem stanu)
#include <Arduino.h>
#include "sensor.h"
#include "network.h"

const int RELAY_PIN = 5;
bool isCoolerOn = false;

// Zmienne do obsługi interwałów czasowych bez użycia delay()
unsigned long previousTempMillis = 0;
unsigned long previousStateMillis = 0;
const long tempInterval = 30000;   // 30 sekund
const long stateInterval = 300000; // 5 minut

void setup()
{
  Serial.begin(115200);
  pinMode(RELAY_PIN, OUTPUT);
  digitalWrite(RELAY_PIN, LOW);
  isCoolerOn = false;

  setupSensor();
  setupNetwork();
}

void loop()
{
  // Ta funkcja musi być wywoływana tak często, jak to możliwe
  loopNetwork();

  unsigned long currentMillis = millis(); // Pobieramy aktualny "czas" od startu programu

  // --- Blok obsługi temperatury i sterowania (co 30 sekund) ---
  if (currentMillis - previousTempMillis >= tempInterval)
  {
    previousTempMillis = currentMillis; // Resetujemy timer

    float currentTemperature = readTemperature();
    if (currentTemperature != -127.0)
    {
      Serial.printf("Current: %.2f C, Target: %.2f C\n", currentTemperature, targetTemperature);
      publishTemperature(currentTemperature);

      bool shouldBeOn = isCoolerOn;
      if (currentTemperature > (targetTemperature + hysteresis))
      {
        shouldBeOn = true;
      }
      else if (currentTemperature < (targetTemperature - hysteresis))
      {
        shouldBeOn = false;
      }

      if (shouldBeOn != isCoolerOn)
      {
        isCoolerOn = shouldBeOn;
        digitalWrite(RELAY_PIN, isCoolerOn ? HIGH : LOW);
        Serial.printf("Cooler state CHANGED to -> %s\n", isCoolerOn ? "ON" : "OFF");
        publishCoolerState(isCoolerOn ? "ON" : "OFF");
        previousStateMillis = currentMillis; // Resetujemy też timer stanu po zmianie
      }
    }
  }

  // --- Blok okresowego raportowania stanu (co 5 minut) ---
  if (currentMillis - previousStateMillis >= stateInterval)
  {
    previousStateMillis = currentMillis; // Resetujemy timer
    Serial.printf("Periodic state report. Cooler is %s\n", isCoolerOn ? "ON" : "OFF");
    publishCoolerState(isCoolerOn ? "ON" : "OFF");
  }
}