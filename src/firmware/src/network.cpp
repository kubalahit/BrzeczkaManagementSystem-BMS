// Plik: src/firmware/src/network.cpp
#include "network.h"
#include <WiFi.h>
#include <DNSServer.h>
#include <WebServer.h>
#include <WiFiManager.h>
#include <PubSubClient.h>
#include <Arduino.h>
#include <Preferences.h> // <-- Nowa biblioteka do pamięci NVS

// === Zmienne globalne ===
float targetTemperature = 19.0; // Wartość domyślna
float hysteresis = 0.5;         // Wartość domyślna
char mqtt_server[40];

const int mqtt_port = 1883;
const char *status_topic = "bms/status/chamber01";
const char *temp_topic = "bms/telemetry/chamber01/temperature";
const char *state_topic = "bms/telemetry/chamber01/cooler_state";
const char *setpoint_topic = "bms/control/chamber01/setpoint";
const char *mqtt_client_id = "chamber01";

WiFiClient espClient;
PubSubClient mqttClient(espClient);
Preferences preferences; // Obiekt do zarządzania pamięcią

// --- NOWA FUNKCJA: Callback dla przychodzących wiadomości MQTT ---
void callback(char *topic, byte *payload, unsigned int length)
{
    Serial.print("Message arrived [");
    Serial.print(topic);
    Serial.print("] ");

    // Konwertujemy payload na string
    char message[length + 1];
    memcpy(message, payload, length);
    message[length] = '\0';
    Serial.println(message);

    // Sprawdzamy, czy to temat, na który czekamy
    if (strcmp(topic, setpoint_topic) == 0)
    {
        // Dzielimy wiadomość "temp:histereza" na dwie części
        char *tempPart = strtok(message, ":");
        char *hysPart = strtok(NULL, ":");

        if (tempPart != NULL && hysPart != NULL)
        {
            float newTemp = atof(tempPart);
            float newHys = atof(hysPart);

            // Aktualizujemy zmienne globalne
            targetTemperature = newTemp;
            hysteresis = newHys;

            // Zapisujemy nowe ustawienia w pamięci nieulotnej
            preferences.begin("bms-settings", false);
            preferences.putFloat("targetTemp", targetTemperature);
            preferences.putFloat("hysteresis", hysteresis);
            preferences.end();

            Serial.println("New settings saved!");
            Serial.printf("Target: %.2f C, Hysteresis: %.2f C\n", targetTemperature, hysteresis);
        }
    }
}

void reconnect()
{
    while (!mqttClient.connected())
    {
        Serial.print("Attempting MQTT connection...");
        if (mqttClient.connect(mqtt_client_id, NULL, NULL, status_topic, 1, true, "OFFLINE"))
        {
            Serial.println("connected");
            // Po połączeniu, subskrybujemy nasz temat kontrolny
            mqttClient.subscribe(setpoint_topic);
            Serial.print("Subscribed to: ");
            Serial.println(setpoint_topic);
        }
        else
        {
            Serial.print("failed, rc=");
            Serial.print(mqttClient.state());
            Serial.println(" try again in 5 seconds");
            delay(5000);
        }
    }
}

void setupNetwork()
{
    // Otwieramy przestrzeń w pamięci i wczytujemy zapisane ustawienia
    preferences.begin("bms-settings", true);                      // true = read-only
    targetTemperature = preferences.getFloat("targetTemp", 19.0); // Wczytaj lub użyj 19.0
    hysteresis = preferences.getFloat("hysteresis", 0.5);         // Wczytaj lub użyj 0.5
    preferences.end();

    Serial.printf("Loaded settings - Target: %.2f C, Hysteresis: %.2f C\n", targetTemperature, hysteresis);

    WiFiManager wm;
    WiFiManagerParameter custom_mqtt_server("mqtt_server", "Adres IP brokera MQTT", "192.168.1.100", 40);
    wm.addParameter(&custom_mqtt_server);

    if (!wm.autoConnect("BMS-Konfiguracja"))
    {
        ESP.restart();
    }

    strcpy(mqtt_server, custom_mqtt_server.getValue());

    mqttClient.setServer(mqtt_server, mqtt_port);
    mqttClient.setCallback(callback); // <-- Rejestrujemy naszą funkcję callback
}

void loopNetwork()
{
    if (!mqttClient.connected())
    {
        reconnect();
    }
    mqttClient.loop();
}

void publishTemperature(float temp)
{
    if (mqttClient.connected())
    {
        char tempString[8];
        dtostrf(temp, 4, 2, tempString);
        mqttClient.publish(temp_topic, tempString);
    }
}

void publishCoolerState(const char *state)
{
    if (mqttClient.connected())
    {
        mqttClient.publish(state_topic, state);
    }
}