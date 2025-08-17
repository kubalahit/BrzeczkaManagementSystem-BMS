// Plik: src/firmware/src/network.cpp
#include "network.h"
#include <WiFi.h>
#include <DNSServer.h>
#include <WebServer.h>
#include <WiFiManager.h>
#include <PubSubClient.h>
#include <Arduino.h>

char mqtt_server[40] = "192.168.1.100";
const int mqtt_port = 1883;
const char *mqtt_topic = "bms/telemetry/chamber01/temperature";
const char *mqtt_client_id = "chamber01";

WiFiClient espClient;
PubSubClient mqttClient(espClient);

void reconnect()
{
    while (!mqttClient.connected())
    {
        Serial.print("Attempting MQTT connection...");
        if (mqttClient.connect(mqtt_client_id))
        {
            Serial.println("connected");
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
    WiFiManager wm;
    WiFiManagerParameter custom_mqtt_server("mqtt_server", "Adres IP brokera MQTT", mqtt_server, 40);
    wm.addParameter(&custom_mqtt_server);

    if (!wm.autoConnect("BMS-Konfiguracja"))
    {
        Serial.println("Failed to connect and hit timeout");
        ESP.restart();
    }

    Serial.println("\nWiFi connected!");
    Serial.print("IP address: ");
    Serial.println(WiFi.localIP());

    strcpy(mqtt_server, custom_mqtt_server.getValue());
    Serial.print("MQTT Broker IP set to: ");
    Serial.println(mqtt_server);

    mqttClient.setServer(mqtt_server, mqtt_port);
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
        mqttClient.publish(mqtt_topic, tempString);
    }
}