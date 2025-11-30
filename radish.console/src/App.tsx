import { useEffect, useState } from 'react';
import './App.css';

interface Service {
    key: string;
    name: string;
    description: string;
    // Gateway 暴露的路径（相对网关根路径）
    path: string;
    // 下游实际目标地址（本地开发时用于参考）
    target: string;
    // 用于健康检查的路径（相对当前 origin）
    healthPath?: string;
}

type ServiceState = 'pending' | 'ok' | 'fail';

interface ServiceStatus {
    state: ServiceState;
    latency?: number;
    message?: string;
}

const services: Service[] = [
    {
        key: 'gateway',
        name: 'Gateway',
        description: 'Radish Gateway 反向代理与统一入口',
        path: '/server',
        target: 'https://localhost:5000/',
        healthPath: '/healthz'
    },
    {
        key: 'frontend',
        name: 'Frontend (webOS)',
        description: '桌面式主入口，提供应用图标和窗口管理',
        path: '/',
        target: 'https://localhost:5000/',
        healthPath: '/'
    },
    {
        key: 'docs',
        name: 'Docs',
        description: 'VitePress 文档站，通过 Gateway 暴露在 /docs',
        path: '/docs',
        target: 'https://localhost:5000/docs',
        healthPath: '/docs'
    },
    {
        key: 'api',
        name: 'API',
        description: 'Radish.Api 后端服务，通过 Gateway 暴露在 /api',
        path: '/api',
        target: 'https://localhost:5000/api',
        healthPath: '/api/health'
    },
    {
        key: 'scalar',
        name: 'Scalar',
        description: 'API Scalar 可视化文档，通过 Gateway 暴露在 /api/docs',
        path: '/api/docs',
        target: 'https://localhost:5000/api/docs',
        healthPath: '/api/docs'
    },
    {
        key: 'console',
        name: 'Console',
        description: '当前管理控制台前端（radish.console）',
        path: '/console',
        target: 'https://localhost:5000/console',
        healthPath: '/console'
    }
];

function App() {
    const [statuses, setStatuses] = useState<Record<string, ServiceStatus>>({});

    useEffect(() => {
        let cancelled = false;

        const checkService = async (service: Service) => {
            if (!service.healthPath) return;

            setStatuses(prev => ({
                ...prev,
                [service.key]: { state: 'pending' }
            }));

            try {
                const start = performance.now();
                const response = await fetch(service.healthPath, { cache: 'no-store' });
                const duration = Math.round(performance.now() - start);

                if (response.ok || (response.status >= 200 && response.status < 400)) {
                    if (!cancelled) {
                        setStatuses(prev => ({
                            ...prev,
                            [service.key]: {
                                state: 'ok',
                                latency: duration
                            }
                        }));
                    }
                } else {
                    if (!cancelled) {
                        setStatuses(prev => ({
                            ...prev,
                            [service.key]: {
                                state: 'fail',
                                latency: duration,
                                message: `HTTP ${response.status}`
                            }
                        }));
                    }
                }
            } catch {
                if (!cancelled) {
                    setStatuses(prev => ({
                        ...prev,
                        [service.key]: {
                            state: 'fail',
                            message: '无法访问'
                        }
                    }));
                }
            }
        };

        services.forEach(service => {
            void checkService(service);
        });

        return () => {
            cancelled = true;
        };
    }, []);

    const renderStatus = (service: Service): string => {
        if (!service.healthPath) return '未配置';
        const status = statuses[service.key];
        if (!status || status.state === 'pending') return '检测中...';
        if (status.state === 'ok') {
            return status.latency != null ? `可用 · ${status.latency} ms` : '可用';
        }
        return status.message ? `异常 · ${status.message}` : '异常';
    };

    return (
        <div>
            <h1>Radish Console</h1>
            <p>
                这是 Radish 网关下各个子系统的控制台入口视图。在这里可以查看服务路径、下游目标、健康状态，并跳转到对应的页面。
            </p>
            <table className="table" aria-label="service overview">
                <thead>
                    <tr>
                        <th>应用</th>
                        <th>Gateway 路径</th>
                        <th>下游目标地址</th>
                        <th>状态</th>
                        <th>说明</th>
                    </tr>
                </thead>
                <tbody>
                    {services.map(service => (
                        <tr key={service.key}>
                            <td>{service.name}</td>
                            <td>
                                <a href={service.path} target="_blank" rel="noreferrer">
                                    {service.path}
                                </a>
                            </td>
                            <td>
                                <a href={service.target} target="_blank" rel="noreferrer">
                                    {service.target}
                                </a>
                            </td>
                            <td>{renderStatus(service)}</td>
                            <td>{service.description}</td>
                        </tr>
                    ))}
                </tbody>
            </table>
        </div>
    );
}

export default App;
