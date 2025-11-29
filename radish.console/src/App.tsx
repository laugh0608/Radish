import './App.css';

interface Service {
    name: string;
    description: string;
    path: string;
    target: string;
}

const services: Service[] = [
    {
        name: 'Frontend (webOS)',
        description: '桌面式主入口，提供应用图标和窗口管理',
        path: '/',
        target: 'https://localhost:3000/'
    },
    {
        name: 'Docs',
        description: 'VitePress 文档站，通过 Gateway 暴露在 /docs',
        path: '/docs',
        target: 'http://localhost:3001/docs/'
    },
    {
        name: 'API',
        description: 'Radish.Api 后端服务，提供核心业务接口',
        path: '/api',
        target: 'https://localhost:5101/api'
    },
    {
        name: 'Scalar',
        description: 'API Scalar 可视化文档，Gateway 映射到 /scalar',
        path: '/scalar',
        target: 'https://localhost:5101/api/docs'
    },
    {
        name: 'Console',
        description: '当前管理控制台前端（radish.console）',
        path: '/console',
        target: 'https://localhost:3002/'
    },
];

function App() {
    return (
        <div>
            <h1>Radish Console</h1>
            <p>
                这是 Radish 网关下各个子系统的控制台入口视图，后续可以在此集成服务状态、健康检查、
                日志跳转等管理能力。
            </p>
            <table className="table" aria-label="service overview">
                <thead>
                    <tr>
                        <th>应用</th>
                        <th>Gateway 路径</th>
                        <th>下游目标地址</th>
                        <th>说明</th>
                    </tr>
                </thead>
                <tbody>
                    {services.map(service => (
                        <tr key={service.name}>
                            <td>{service.name}</td>
                            <td>{service.path}</td>
                            <td>{service.target}</td>
                            <td>{service.description}</td>
                        </tr>
                    ))}
                </tbody>
            </table>
        </div>
    );
}

export default App;
