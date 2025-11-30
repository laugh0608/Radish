import { useEffect, useMemo, useState } from 'react';
import './App.css';

interface Forecast {
    date: string;
    temperatureC: number;
    temperatureF: number;
    summary: string;
}

// 默认通过 Gateway 暴露的 API 入口
const defaultApiBase = 'https://localhost:5000';

function App() {
    const [forecasts, setForecasts] = useState<Forecast[]>();
    const [error, setError] = useState<string>();
    const apiBaseUrl = useMemo(() => {
        const configured = import.meta.env.VITE_API_BASE_URL as string | undefined;
        return (configured ?? defaultApiBase).replace(/\/$/, '');
    }, []);

    useEffect(() => {
        populateWeatherData();
    }, []);

    const contents = forecasts === undefined
        ? <p><em>{error ?? 'Loading weather data from Radish.Api...'}</em></p>
        : <table className="table table-striped" aria-labelledby="tableLabel">
            <thead>
                <tr>
                    <th>Date</th>
                    <th>Temp. (C)</th>
                    <th>Temp. (F)</th>
                    <th>Summary</th>
                </tr>
            </thead>
            <tbody>
                {forecasts.map(forecast =>
                    <tr key={forecast.date}>
                        <td>{forecast.date}</td>
                        <td>{forecast.temperatureC}</td>
                        <td>{forecast.temperatureF}</td>
                        <td>{forecast.summary}</td>
                    </tr>
                )}
            </tbody>
        </table>;

    return (
        <div>
            <h1 id="tableLabel">Radish Weather Forecast</h1>
            <p>实时展示来自 Radish.Api 的 WeatherForecast 示例数据，便于验证前后端联通性。</p>
            {contents}
        </div>
    );

    async function populateWeatherData() {
        const requestUrl = `${apiBaseUrl}/api/WeatherForecast/Get`;
        try {
            const response = await fetch(requestUrl, {
                headers: {
                    Accept: 'application/json'
                }
            });
            if (!response.ok) {
                throw new Error(`API error: ${response.status} ${response.statusText}`);
            }
            const data = await response.json();
            setForecasts(data);
            setError(undefined);
        } catch (err) {
            const message = err instanceof Error ? err.message : String(err);
            setError(`无法从 ${requestUrl} 获取数据：${message}`);
            setForecasts(undefined);
        }
    }
}

export default App;
