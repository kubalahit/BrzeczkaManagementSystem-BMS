// Plik: src/firmware/src/sensor.cpp
#include "sensor.h"
#include <OneWire.h>
#include <DallasTemperature.h>
#include <Arduino.h>

const int ONE_WIRE_BUS_PIN = 4;
OneWire oneWire(ONE_WIRE_BUS_PIN);
DallasTemperature sensors(&oneWire);

void setupSensor()
{
    sensors.begin();
}

float readTemperature()
{
    sensors.requestTemperatures();
    float temp = sensors.getTempCByIndex(0);
    if (temp == DEVICE_DISCONNECTED_C)
    {
        Serial.println("Error: Could not read temperature data");
        return -127.0; // Zwracamy niemożliwą wartość jako błąd
    }
    return temp;
}