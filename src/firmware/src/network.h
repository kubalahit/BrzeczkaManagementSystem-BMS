// Plik: src/firmware/src/network.h
#ifndef NETWORK_H
#define NETWORK_H

extern float targetTemperature;
extern float hysteresis;

void setupNetwork();
void loopNetwork();
void publishTemperature(float temp);
void publishCoolerState(const char *state);

#endif