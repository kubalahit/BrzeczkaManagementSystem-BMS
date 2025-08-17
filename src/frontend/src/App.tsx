// Plik: src/frontend/src/App.tsx
import React from 'react';
import './App.css';
import CurrentTemperatureDisplay from './components/CurrentTemperatureDisplay';
import TemperatureChart from './components/TemperatureChart';

function App() {
  return (
    <div className="App">
      <header className="App-header">
        <h1>BMS - Brzeczka Management System</h1>
        <CurrentTemperatureDisplay />
        <TemperatureChart />
      </header>
    </div>
  );
}

export default App;