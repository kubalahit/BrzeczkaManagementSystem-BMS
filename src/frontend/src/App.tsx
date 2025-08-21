// Plik: src/frontend/src/App.tsx
import React from 'react';
import './App.css';
import CurrentTemperatureDisplay from './components/CurrentTemperatureDisplay';
import TemperatureChart from './components/TemperatureChart';
import ChamberSettings from './components/ChamberSettings';

function App() {
  return (
    <div className="App">
      <header className="App-header">
        <h1>BMS - Brzeczka Management System</h1>
        <CurrentTemperatureDisplay />
        <ChamberSettings />
        <hr style={{ width: "80%" }} />
        <TemperatureChart />
      </header>
    </div>
  );
}

export default App;