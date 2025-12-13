import { useState } from 'react';
import {
  Button,
  Input,
  Select,
  Modal,
  type SelectOption
} from '@radish/ui';
import {
  useDebounce,
  useToggle,
  useLocalStorage
} from '@radish/ui/hooks';
import {
  formatDate,
  formatFileSize,
  isEmail,
  truncate,
  capitalize
} from '@radish/ui/utils';

/**
 * 完整的 @radish/ui 组件库示例
 */
export const UIComponentsExample = () => {
  const [searchTerm, setSearchTerm] = useState('');
  const [email, setEmail] = useState('');
  const [selectedCity, setSelectedCity] = useState('');
  const [isModalOpen, toggleModal, openModal, closeModal] = useToggle(false);
  const [count, setCount] = useLocalStorage('example-count', 0);

  const debouncedSearch = useDebounce(searchTerm, 500);

  const cityOptions: SelectOption[] = [
    { value: 'beijing', label: '北京' },
    { value: 'shanghai', label: '上海' },
    { value: 'guangzhou', label: '广州' },
    { value: 'shenzhen', label: '深圳' }
  ];

  return (
    <div style={{ padding: '20px', maxWidth: '800px' }}>
      <h1>@radish/ui 组件库完整示例</h1>

      {/* Button 组件 */}
      <section style={{ marginBottom: '40px' }}>
        <h2>Button 组件</h2>
        <div style={{ display: 'flex', gap: '10px', flexWrap: 'wrap', marginBottom: '10px' }}>
          <Button variant="primary" onClick={() => setCount(count + 1)}>
            Primary
          </Button>
          <Button variant="secondary" onClick={() => setCount(count - 1)}>
            Secondary
          </Button>
          <Button variant="danger" onClick={() => setCount(0)}>
            Danger
          </Button>
        </div>
        <div style={{ display: 'flex', gap: '10px', flexWrap: 'wrap' }}>
          <Button size="small">Small</Button>
          <Button size="medium">Medium</Button>
          <Button size="large">Large</Button>
          <Button disabled>Disabled</Button>
        </div>
        <p>计数器 (localStorage): {count}</p>
      </section>

      {/* Input 组件 */}
      <section style={{ marginBottom: '40px' }}>
        <h2>Input 组件</h2>
        <div style={{ display: 'flex', flexDirection: 'column', gap: '20px' }}>
          <Input
            label="搜索"
            placeholder="输入搜索词..."
            value={searchTerm}
            onChange={(e) => setSearchTerm(e.target.value)}
            helperText={`防抖后的值: ${debouncedSearch}`}
          />
          <Input
            label="邮箱"
            type="email"
            placeholder="请输入邮箱"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            error={email && !isEmail(email) ? '邮箱格式不正确' : undefined}
            required
          />
          <Input
            label="禁用状态"
            value="不可编辑"
            disabled
          />
        </div>
      </section>

      {/* Select 组件 */}
      <section style={{ marginBottom: '40px' }}>
        <h2>Select 组件</h2>
        <Select
          label="选择城市"
          placeholder="请选择城市"
          options={cityOptions}
          value={selectedCity}
          onChange={(e) => setSelectedCity(e.target.value)}
          helperText="选择你所在的城市"
          required
        />
        {selectedCity && (
          <p>你选择了: {cityOptions.find(c => c.value === selectedCity)?.label}</p>
        )}
      </section>

      {/* Modal 组件 */}
      <section style={{ marginBottom: '40px' }}>
        <h2>Modal 组件</h2>
        <div style={{ display: 'flex', gap: '10px' }}>
          <Button onClick={openModal}>打开模态框</Button>
        </div>
        <Modal
          isOpen={isModalOpen}
          onClose={closeModal}
          title="示例模态框"
          size="medium"
          footer={
            <>
              <Button variant="secondary" onClick={closeModal}>
                取消
              </Button>
              <Button variant="primary" onClick={closeModal}>
                确定
              </Button>
            </>
          }
        >
          <p>这是一个模态框的内容区域。</p>
          <p>你可以在这里放置任何内容。</p>
          <ul>
            <li>支持自定义标题</li>
            <li>支持自定义底部按钮</li>
            <li>支持点击遮罩层关闭</li>
            <li>支持 ESC 键关闭</li>
            <li>支持三种尺寸: small, medium, large</li>
          </ul>
        </Modal>
      </section>

      {/* 工具函数示例 */}
      <section style={{ marginBottom: '40px' }}>
        <h2>工具函数</h2>
        <div style={{ background: '#f5f5f5', padding: '15px', borderRadius: '8px' }}>
          <h3>日期格式化</h3>
          <ul>
            <li>默认格式: {formatDate(new Date())}</li>
            <li>自定义格式: {formatDate(new Date(), 'YYYY/MM/DD')}</li>
          </ul>

          <h3>文件大小格式化</h3>
          <ul>
            <li>1024 字节: {formatFileSize(1024)}</li>
            <li>1048576 字节: {formatFileSize(1048576)}</li>
            <li>1073741824 字节: {formatFileSize(1073741824)}</li>
          </ul>

          <h3>字符串处理</h3>
          <ul>
            <li>截断: {truncate('这是一个很长的字符串，需要被截断', 15)}</li>
            <li>首字母大写: {capitalize('hello world')}</li>
          </ul>

          <h3>验证函数</h3>
          <ul>
            <li>test@example.com 是邮箱: {isEmail('test@example.com') ? '✓' : '✗'}</li>
            <li>invalid-email 是邮箱: {isEmail('invalid-email') ? '✓' : '✗'}</li>
          </ul>
        </div>
      </section>

      {/* Hooks 示例 */}
      <section>
        <h2>Hooks</h2>
        <div style={{ background: '#f5f5f5', padding: '15px', borderRadius: '8px' }}>
          <h3>useDebounce</h3>
          <p>实时输入: {searchTerm}</p>
          <p>防抖后 (500ms): {debouncedSearch}</p>

          <h3>useToggle</h3>
          <p>模态框状态: {isModalOpen ? '打开' : '关闭'}</p>

          <h3>useLocalStorage</h3>
          <p>计数器值会持久化到 localStorage</p>
        </div>
      </section>
    </div>
  );
};
