// Plik: src/frontend/src/components/CurrentTemperatureDisplay.tsx
import React, { useState, useEffect } from 'react';

interface TemperatureData {
    time: string;
    value: number;
}

const CurrentTemperatureDisplay: React.FC = () => {
    const [data, setData] = useState<TemperatureData | null>(null);
    const [loading, setLoading] = useState<boolean>(true);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        const fetchData = async () => {
            try {
                const response = await fetch('/api/temperature/latest');
                if (!response.ok) {
                    throw new Error(`Błąd HTTP! Status: ${response.status}`);
                }
                const result = await response.json();
                setData(result);
                setError(null);
            } catch (e: any) {
                setError(e.message);
                console.error("Błąd podczas pobierania danych:", e);
            } finally {
                setLoading(false);
            }
        };

        fetchData(); // Uruchamiamy od razu
        const intervalId = setInterval(fetchData, 10000); // Odświeżamy co 10 sekund
        return () => clearInterval(intervalId); // Czyszczenie po usunięciu komponentu
    }, []);

    if (loading) return <div><h2>Aktualna Temperatura</h2><p>Ładowanie...</p></div>;
    if (error) return <div><h2>Aktualna Temperatura</h2><p style={{ color: 'red' }}>Błąd: {error}</p></div>;

    return (
        <div>
            <h2>Aktualna Temperatura</h2>
            {data ? (
                <h1>{data.value.toFixed(2)} °C</h1>
            ) : (
                <p>Brak danych.</p>
            )}
        </div>
    );
};

export default CurrentTemperatureDisplay;