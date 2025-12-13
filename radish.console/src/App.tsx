import { useEffect, useState } from 'react';
import './App.css';
import { UIComponentsExample } from './examples/UIComponentsExample';
import { Button } from '@radish/ui';

interface Service {
    key: string;
    name: string;
    description: string;
    // Gateway 暴露的路径（相对网关根路径）
    gatewayPath: string;
    // Gateway 完整 URL（开发环境）
    gatewayUrl: string;
    // 内部服务地址（本地开发时的实际端口）
    internalUrl?: string;
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
        gatewayPath: '/server',
        gatewayUrl: 'https://localhost:5000/server',
        internalUrl: 'https://localhost:5000',
        healthPath: '/healthz'
    },
    {
        key: 'frontend',
        name: 'Frontend (WebOS)',
        description: '桌面式主入口，提供应用图标和窗口管理',
        gatewayPath: '/',
        gatewayUrl: 'https://localhost:5000/',
        internalUrl: 'http://localhost:3000',
        healthPath: '/'
    },
    {
        key: 'docs',
        name: 'Docs',
        description: 'VitePress 文档站，通过 Gateway 暴露',
        gatewayPath: '/docs',
        gatewayUrl: 'https://localhost:5000/docs',
        internalUrl: 'http://localhost:3001',
        healthPath: '/docs'
    },
    {
        key: 'api',
        name: 'API',
        description: 'Radish.Api 后端服务，提供 RESTful API',
        gatewayPath: '/api',
        gatewayUrl: 'https://localhost:5000/api',
        internalUrl: 'http://localhost:5100',
        healthPath: '/api/health'
    },
    {
        key: 'auth',
        name: 'Auth (OIDC)',
        description: 'Radish.Auth OIDC 认证服务器，基于 OpenIddict',
        gatewayPath: '/auth',
        gatewayUrl: 'https://localhost:5000/auth',
        internalUrl: 'http://localhost:5200',
        healthPath: undefined // Auth 暂时没有暴露健康检查端点
    },
    {
        key: 'scalar',
        name: 'Scalar',
        description: 'API Scalar 可视化文档，交互式 API 调试工具',
        gatewayPath: '/scalar',
        gatewayUrl: 'https://localhost:5000/scalar',
        internalUrl: undefined,
        healthPath: '/scalar'
    },
    {
        key: 'console',
        name: 'Console',
        description: '当前管理控制台前端（radish.console）',
        gatewayPath: '/console',
        gatewayUrl: 'https://localhost:5000/console',
        internalUrl: 'http://localhost:3002',
        healthPath: '/console'
    }
];

function App() {
    const [statuses, setStatuses] = useState<Record<string, ServiceStatus>>({});
    const [showUITest, setShowUITest] = useState(false);

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

    const getStatusIcon = (service: Service): string => {
        if (!service.healthPath) return '⚪';
        const status = statuses[service.key];
        if (!status || status.state === 'pending') return '🔵';
        if (status.state === 'ok') return '🟢';
        return '🔴';
    };

    const renderStatus = (service: Service): string => {
        if (!service.healthPath) return '未配置检查';
        const status = statuses[service.key];
        if (!status || status.state === 'pending') return '检测中...';
        if (status.state === 'ok') {
            return status.latency != null ? `${status.latency} ms` : '正常';
        }
        return status.message ? status.message : '异常';
    };

    const getStatusClass = (service: Service): string => {
        if (!service.healthPath) return 'status-unchecked';
        const status = statuses[service.key];
        if (!status || status.state === 'pending') return 'status-pending';
        if (status.state === 'ok') return 'status-ok';
        return 'status-fail';
    };

    // 如果显示 UI 测试页面，则渲染测试组件
    if (showUITest) {
        return (
            <div className="container">
                <div style={{ padding: '20px' }}>
                    <Button
                        variant="secondary"
                        onClick={() => setShowUITest(false)}
                        style={{ marginBottom: '20px' }}
                    >
                        ← 返回服务状态页面
                    </Button>
                    <UIComponentsExample />
                </div>
            </div>
        );
    }

    return (
        <div className="container">
            <header className="header">
                <h1>🌿 Radish Console</h1>
                <p className="subtitle">
                    Radish 微服务控制台 - 查看服务状态、路径配置与健康检查
                </p>
                <div style={{ marginTop: '15px' }}>
                    <Button
                        variant="primary"
                        onClick={() => setShowUITest(true)}
                    >
                        查看 UI 组件测试页面
                    </Button>
                </div>
            </header>

            <div className="services-table">
                <table aria-label="service overview">
                    <thead>
                        <tr>
                            <th>状态</th>
                            <th>服务名称</th>
                            <th>Gateway 路径</th>
                            <th>内部地址</th>
                            <th>健康检查</th>
                            <th>说明</th>
                        </tr>
                    </thead>
                    <tbody>
                        {services.map(service => (
                            <tr key={service.key}>
                                <td className="status-icon">
                                    <span className={getStatusClass(service)} title={renderStatus(service)}>
                                        {getStatusIcon(service)}
                                    </span>
                                </td>
                                <td className="service-name">
                                    <strong>{service.name}</strong>
                                </td>
                                <td className="service-url">
                                    <a
                                        href={service.gatewayUrl}
                                        target="_blank"
                                        rel="noreferrer"
                                        title={service.gatewayUrl}
                                    >
                                        {service.gatewayPath}
                                    </a>
                                </td>
                                <td className="service-url">
                                    {service.internalUrl ? (
                                        <a
                                            href={service.internalUrl}
                                            target="_blank"
                                            rel="noreferrer"
                                            title={service.internalUrl}
                                        >
                                            {service.internalUrl.replace('http://', '').replace('https://', '')}
                                        </a>
                                    ) : (
                                        <span className="text-muted">-</span>
                                    )}
                                </td>
                                <td className={`health-status ${getStatusClass(service)}`}>
                                    {renderStatus(service)}
                                </td>
                                <td className="service-description">
                                    {service.description}
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>

            <footer className="footer">
                <p>
                    <strong>端口约定：</strong>
                    Gateway (5000/5001) · API (5100) · Auth (5200) · Frontend (3000) · Docs (3001) · Console (3002)
                </p>
                <p className="text-muted">
                    所有对外访问通过 Gateway (https://localhost:5000) 统一入口，内部服务仅用于开发调试
                </p>
            </footer>
        </div>
    );
}

export default App;
