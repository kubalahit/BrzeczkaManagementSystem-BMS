// Plik: src/firmware/src/network.cpp
#include "network.h"
#include <WiFi.h>
#include <DNSServer.h>
#include <WebServer.h>
#include <WiFiManager.h>
#include <PubSubClient.h>
#include <Arduino.h>
#include <Preferences.h>
#include <ESPmDNS.h>
#include <ArduinoJson.h>

// Definicja pinu przycisku "BOOT"
const int CONFIG_BUTTON_PIN = 0;

// === Zmienne globalne ===
float targetTemperature = 19.0;
float hysteresis = 0.5;
char mqtt_server[40];
char mqtt_client_id[32] = "chamber01"; // Domyślne ID
// Bufory na dynamiczne tematy
char status_topic[64];
char temp_topic[64];
char state_topic[64];
char setpoint_topic[64];

const int mqtt_port = 1883;

WiFiClient espClient;
PubSubClient mqttClient(espClient);
Preferences preferences;

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
        Serial.print("Attempting MQTT connection on IP: ");
        Serial.print(mqtt_server);
        Serial.print(" ... ");
        if (mqttClient.connect(mqtt_client_id, NULL, NULL, status_topic, 1, true, "OFFLINE"))
        {
            Serial.println("connected");
            mqttClient.publish(status_topic, "ONLINE", true);
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
    // 1. Zawsze wczytujemy ostatnią znaną konfigurację z pamięci
    preferences.begin("bms-settings", false);
    preferences.getString("mqttServer", mqtt_server, sizeof(mqtt_server));
    preferences.getString("clientId", mqtt_client_id, sizeof(mqtt_client_id));
    // Wczytujemy też zapisane nastawy
    targetTemperature = preferences.getFloat("targetTemp", 19.0);
    hysteresis = preferences.getFloat("hysteresis", 0.5);
    preferences.end();

    WiFi.mode(WIFI_STA);
    WiFi.begin();
    Serial.print("Attempting to connect to last known WiFi...");

    int connect_timeout = 15;
    while (WiFi.status() != WL_CONNECTED && connect_timeout > 0)
    {
        delay(1000);
        Serial.print(".");
        connect_timeout--;
    }

    bool needsConfig = (WiFi.status() != WL_CONNECTED || strlen(mqtt_server) == 0);

    if (needsConfig)
    {
        Serial.println("\nConfiguration needed. Starting WiFiManager & Discovery Mode...");

        WiFiManager wm;
        if (!wm.autoConnect("BMS-Konfiguracja"))
        {
            ESP.restart();
        }

        String mac = WiFi.macAddress();
        mac.replace(":", "");
        String instanceName = "BMS-Chamber-" + mac;

        if (MDNS.begin(instanceName.c_str()))
        {
            MDNS.addService("_bms-chamber", "_tcp", 80);
            Serial.println("Service advertised: _bms-chamber._tcp.local");
        }

        WebServer server(80);
        server.on("/configure", HTTP_POST, [&]()
                  {
            String body = server.arg("plain");
            
            JsonDocument doc;
            deserializeJson(doc, body);

            preferences.begin("bms-settings", false);
            preferences.putString("mqttServer", doc["mqtt_server"].as<const char*>());
            preferences.putString("clientId", doc["client_id"].as<const char*>());
            preferences.putString("statusTopic", doc["status_topic"].as<const char*>());
            preferences.putString("tempTopic", doc["temp_topic"].as<const char*>());
            preferences.putString("stateTopic", doc["state_topic"].as<const char*>());
            preferences.putString("setpointTopic", doc["setpoint_topic"].as<const char*>());
            preferences.end();
            
            server.send(200, "text/plain", "OK. Restarting.");
            delay(1000);
            ESP.restart(); });
        server.begin();

        Serial.println("Waiting for configuration from BMS server...");
        while (true)
        {
            server.handleClient();
            // MDNS.update() nie jest potrzebne w tej pętli
            delay(100);
        }
    }

    // Jeśli konfiguracja jest OK, budujemy tematy i kontynuujemy
    preferences.begin("bms-settings", true);
    snprintf(status_topic, sizeof(status_topic), "bms/status/%s", mqtt_client_id);
    snprintf(temp_topic, sizeof(temp_topic), "bms/telemetry/%s/temperature", mqtt_client_id);
    snprintf(state_topic, sizeof(state_topic), "bms/telemetry/%s/cooler_state", mqtt_client_id);
    snprintf(setpoint_topic, sizeof(setpoint_topic), "bms/control/%s/setpoint", mqtt_client_id);
    preferences.end();

    Serial.println("\nWiFi connected!");
    Serial.printf("IP address: %s\n", WiFi.localIP().toString().c_str());
    Serial.printf("MQTT Broker: %s\n", mqtt_server);

    mqttClient.setServer(mqtt_server, mqtt_port);
    mqttClient.setCallback(callback);
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