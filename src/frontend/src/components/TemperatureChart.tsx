// Plik: src/frontend/src/components/TemperatureChart.tsx
import React, { useState, useEffect } from 'react';
import { Line } from 'react-chartjs-2';
import {
    Chart as ChartJS,
    CategoryScale,
    LinearScale,
    PointElement,
    LineElement,
    Title,
    Tooltip,
    Legend,
} from 'chart.js';

// Rejestrujemy komponenty, których będziemy używać w wykresie
ChartJS.register(
    CategoryScale,
    LinearScale,
    PointElement,
    LineElement,
    Title,
    Tooltip,
    Legend
);

// Ponownie, definiujemy "kontrakt" na nasze dane
interface TemperatureHistoryPoint {
    time: string;
    value: number;
}

const TemperatureChart: React.FC = () => {
    // Stan do przechowywania danych wykresu
    const [chartData, setChartData] = useState<any>({});
    const [loading, setLoading] = useState<boolean>(true);

    useEffect(() => {
        const fetchHistory = async () => {
            try {
                // Pobieramy dane z naszego API (z ostatniej godziny)
                const response = await fetch('/api/temperature/history?range=1h');
                if (!response.ok) {
                    throw new Error(`Błąd HTTP! Status: ${response.status}`);
                }
                const result: TemperatureHistoryPoint[] = await response.json();

                // Przetwarzamy dane z API na format zrozumiały dla biblioteki Chart.js
                const labels = result.map(point => new Date(point.time).toLocaleTimeString());
                const dataPoints = result.map(point => point.value);

                // Aktualizujemy stan komponentu o nowe dane wykresu
                setChartData({
                    labels: labels,
                    datasets: [
                        {
                            label: 'Temperatura (°C)',
                            data: dataPoints,
                            borderColor: 'rgb(75, 192, 192)',
                            tension: 0.1,
                        },
                    ],
                });

            } catch (e) {
                console.error("Błąd podczas pobierania historii:", e);
            } finally {
                setLoading(false);
            }
        };

        fetchHistory();
        const intervalId = setInterval(fetchHistory, 60000); // Odświeżamy co 10 sekund
        return () => clearInterval(intervalId); // Czyszczenie po usunięciu komponentu
    }, []);

    if (loading) return <div>Ładowanie wykresu...</div>;

    return (
        <div style={{ width: '80%', margin: 'auto' }}>
            <h3>Historia Temperatury (ostatnia godzina)</h3>
            {chartData.labels ? <Line data={chartData} /> : <p>Brak danych do wyświetlenia.</p>}
        </div>
    );
};

export default TemperatureChart;