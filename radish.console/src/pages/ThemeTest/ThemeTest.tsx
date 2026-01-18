import {
  AntButton,
  Space,
  Tag,
  Badge,
  Tooltip,
  Avatar,
  Divider,
  UserOutlined,
} from '@radish/ui';
import './ThemeTest.css';

/**
 * 主题测试页面
 *
 * 用于验证 Ant Design 主题配置是否正确
 */
export function ThemeTest() {
  return (
    <div className="theme-test">
      <h1>Radish 主题测试</h1>

      <Divider>按钮组件</Divider>
      <Space>
        <AntButton type="primary">主要按钮</AntButton>
        <AntButton>默认按钮</AntButton>
        <AntButton type="dashed">虚线按钮</AntButton>
        <AntButton type="text">文本按钮</AntButton>
        <AntButton type="link">链接按钮</AntButton>
        <AntButton danger>危险按钮</AntButton>
      </Space>

      <Divider>标签组件</Divider>
      <Space>
        <Tag color="default">默认标签</Tag>
        <Tag color="success">成功标签</Tag>
        <Tag color="warning">警告标签</Tag>
        <Tag color="error">错误标签</Tag>
        <Tag color="processing">处理中</Tag>
      </Space>

      <Divider>徽章组件</Divider>
      <Space size="large">
        <Badge count={5}>
          <Avatar shape="square" size="large" />
        </Badge>
        <Badge count={0} showZero>
          <Avatar shape="square" size="large" />
        </Badge>
        <Badge dot>
          <Avatar shape="square" size="large" />
        </Badge>
      </Space>

      <Divider>提示框组件</Divider>
      <Space>
        <Tooltip title="这是一个提示">
          <AntButton>悬停查看提示</AntButton>
        </Tooltip>
        <Tooltip title="这是一个很长很长很长很长很长很长的提示文本">
          <AntButton>长提示</AntButton>
        </Tooltip>
      </Space>

      <Divider>头像组件</Divider>
      <Space>
        <Avatar size={64} icon={<UserOutlined />} />
        <Avatar size="large" icon={<UserOutlined />} />
        <Avatar icon={<UserOutlined />} />
        <Avatar size="small" icon={<UserOutlined />} />
      </Space>

      <Divider>主题色展示</Divider>
      <div className="color-palette">
        <div className="color-item">
          <div className="color-box" style={{ backgroundColor: '#FF6B35' }} />
          <div className="color-name">主色调</div>
          <div className="color-value">#FF6B35</div>
        </div>
        <div className="color-item">
          <div className="color-box" style={{ backgroundColor: '#52c41a' }} />
          <div className="color-name">成功色</div>
          <div className="color-value">#52c41a</div>
        </div>
        <div className="color-item">
          <div className="color-box" style={{ backgroundColor: '#faad14' }} />
          <div className="color-name">警告色</div>
          <div className="color-value">#faad14</div>
        </div>
        <div className="color-item">
          <div className="color-box" style={{ backgroundColor: '#ff4d4f' }} />
          <div className="color-name">错误色</div>
          <div className="color-value">#ff4d4f</div>
        </div>
        <div className="color-item">
          <div className="color-box" style={{ backgroundColor: '#1890ff' }} />
          <div className="color-name">信息色</div>
          <div className="color-value">#1890ff</div>
        </div>
      </div>
    </div>
  );
}
