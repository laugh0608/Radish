import { useDocumentTitle } from '@/hooks/useDocumentTitle';
import './Dashboard.css';

export const Dashboard = () => {
  useDocumentTitle('仪表盘');
  return (
    <div className="dashboard-page">
      <h2>仪表盘</h2>
      <p>欢迎使用 Radish Console 后台管理系统</p>

      <div style={{ marginTop: '24px' }}>
        <h3>快速导航</h3>
        <ul>
          <li>应用管理 - 管理 OIDC 客户端应用</li>
          <li>用户管理 - 管理系统用户（待实现）</li>
          <li>角色管理 - 管理用户角色和权限（待实现）</li>
        </ul>
      </div>
    </div>
  );
};
