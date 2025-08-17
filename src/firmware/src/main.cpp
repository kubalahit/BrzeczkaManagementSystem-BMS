// Plik: src/firmware/src/main.cpp (wersja po refaktoryzacji)
#include <Arduino.h>
#include "sensor.h"
#include "network.h"

void setup()
{
  Serial.begin(115200);
  Serial.println("BMS Firmware Starting...");

  setupSensor();
  setupNetwork();
}

void loop()
{
  loopNetwork(); // Pozwól klientowi MQTT działać w tle

  float currentTemperature = readTemperature();

  if (currentTemperature != -127.0)
  { // Sprawdzamy czy nie ma błędu odczytu
    Serial.print("Temperature is: ");
    Serial.print(currentTemperature);
    Serial.println(" °C. Publishing...");
    publishTemperature(currentTemperature);
  }

  delay(30000);
}