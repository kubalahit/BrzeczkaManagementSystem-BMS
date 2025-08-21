// Plik: src/frontend/src/components/ChamberSettings.tsx (nowa, inteligentna wersja)
import React, { useState, useEffect } from 'react';

// Definiujemy, jak wyglądają dane o ustawieniach
interface ChamberSettingsData {
    targetTemperature: number;
    hysteresis: number;
}

const ChamberSettings: React.FC = () => {
    // Stan dla wartości w formularzu
    const [targetTemp, setTargetTemp] = useState<string>("");
    const [hysteresis, setHysteresis] = useState<string>("");

    // Stan do obsługi komunikacji z API
    const [statusMessage, setStatusMessage] = useState<string>("");
    const [isLoading, setIsLoading] = useState<boolean>(true); // <-- Nowy stan do ładowania

    // --- NOWA LOGIKA: Pobieranie aktualnych ustawień przy starcie ---
    useEffect(() => {
        const fetchCurrentSettings = async () => {
            try {
                // Odpytujemy API o aktualne ustawienia dla komory nr 1
                const response = await fetch('/api/chamber/1');
                if (!response.ok) {
                    throw new Error(`Błąd HTTP! Status: ${response.status}`);
                }
                const result: ChamberSettingsData = await response.json();

                // Ustawiamy wartości w formularzu na podstawie danych z API
                setTargetTemp(result.targetTemperature.toString());
                setHysteresis(result.hysteresis.toString());

            } catch (e: any) {
                setStatusMessage(`Błąd ładowania ustawień: ${e.message}`);
            } finally {
                setIsLoading(false); // Kończymy ładowanie
            }
        };

        fetchCurrentSettings();
    }, []); // Pusta tablica [] oznacza, że efekt uruchomi się tylko raz

    // Funkcja do zapisu (bez większych zmian)
    const handleSubmit = async (event: React.FormEvent) => {
        event.preventDefault();
        setStatusMessage("Zapisywanie...");
        try {
            const settings = {
                targetTemperature: parseFloat(targetTemp),
                hysteresis: parseFloat(hysteresis)
            };
            const response = await fetch('/api/chamber/1/settings', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(settings),
            });
            if (!response.ok) {
                throw new Error(`Błąd HTTP! Status: ${response.status}`);
            }
            setStatusMessage("Ustawienia zapisane pomyślnie!");
        } catch (e: any) {
            setStatusMessage(`Błąd zapisu: ${e.message}`);
        }
    };

    // Jeśli ładujemy dane, wyświetlamy informację
    if (isLoading) {
        return <div><h2>Ustawienia Komory</h2><p>Ładowanie aktualnych ustawień...</p></div>;
    }

    return (
        <div style={{ marginTop: '2rem' }}>
            <h2>Ustawienia Komory</h2>
            <form onSubmit={handleSubmit}>
                <div>
                    <label>
                        Temperatura docelowa (°C):&nbsp;
                        <input
                            type="number"
                            step="0.1"
                            value={targetTemp}
                            onChange={(e) => setTargetTemp(e.target.value)}
                        />
                    </label>
                </div>
                <div style={{ marginTop: '0.5rem' }}>
                    <label>
                        Histereza (°C):&nbsp;
                        <input
                            type="number"
                            step="0.1"
                            value={hysteresis}
                            onChange={(e) => setHysteresis(e.target.value)}
                        />
                    </label>
                </div>
                <button type="submit" style={{ marginTop: '1rem' }}>
                    Zapisz ustawienia
                </button>
            </form>
            {statusMessage && <p>{statusMessage}</p>}
        </div>
    );
};

export default ChamberSettings;