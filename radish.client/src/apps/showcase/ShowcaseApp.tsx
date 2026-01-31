import { useState } from 'react';
import {
  Icon,
  Button,
  GlassPanel,
  ContextMenu,
  Input,
  Select,
  Modal,
  ConfirmDialog,
  Toast,
  toast,
  ExperienceBar,
  TableSkeleton,
  SimpleSkeleton,
  CardSkeleton,
  LineChart,
  BarChart,
  PieChart,
  AreaChart,
  type ExperienceData
} from '@radish/ui';
import styles from './ShowcaseApp.module.css';

/**
 * 组件库展示应用
 *
 * 展示 @radish/ui 中的所有组件
 */
export const ShowcaseApp = () => {
  const [activeSection, setActiveSection] = useState<string>('all');
  const [inputValue, setInputValue] = useState('');
  const [selectValue, setSelectValue] = useState('');
  const [modalOpen, setModalOpen] = useState(false);
  const [confirmOpen, setConfirmOpen] = useState(false);

  // 模拟经验数据（使用 Vo 前缀字段名）
  const mockExperienceData: ExperienceData = {
    voUserId: 1,
    voCurrentLevel: 5,
    voLevelName: '萝卜达人',
    voCurrentExp: 2500,
    voTotalExp: 12500,
    voNextLevelExp: 3000,
    voExpToNextLevel: 500,
    voNextLevelName: '萝卜专家',
    voLevelProgress: 83,
    voThemeColor: '#667eea',
    voRank: 42,
    voExpFrozen: false
  };

  // 模拟图表数据
  const lineChartData = [
    { date: '1/25', value: 120 },
    { date: '1/26', value: 180 },
    { date: '1/27', value: 150 },
    { date: '1/28', value: 220 },
    { date: '1/29', value: 190 },
    { date: '1/30', value: 280 },
    { date: '1/31', value: 250 }
  ];

  const barChartData = [
    { name: '发帖', value: 45 },
    { name: '评论', value: 78 },
    { name: '点赞', value: 120 },
    { name: '收藏', value: 32 }
  ];

  const pieChartData = [
    { name: '发帖', value: 35, color: '#667eea' },
    { name: '评论', value: 25, color: '#764ba2' },
    { name: '点赞', value: 30, color: '#f093fb' },
    { name: '其他', value: 10, color: '#4facfe' }
  ];

  const areaChartData = [
    { date: '1/25', users: 100, posts: 50 },
    { date: '1/26', users: 120, posts: 65 },
    { date: '1/27', users: 115, posts: 58 },
    { date: '1/28', users: 140, posts: 72 },
    { date: '1/29', users: 135, posts: 68 },
    { date: '1/30', users: 160, posts: 85 },
    { date: '1/31', users: 155, posts: 80 }
  ];

  const sections = [
    { id: 'all', name: '全部' },
    { id: 'basic', name: '基础组件' },
    { id: 'form', name: '表单组件' },
    { id: 'feedback', name: '反馈组件' },
    { id: 'data', name: '数据展示' },
    { id: 'charts', name: '图表组件' }
  ];

  const shouldShow = (section: string) => activeSection === 'all' || activeSection === section;

  return (
    <div className={styles.showcase}>
      <div className={styles.container}>
        <h1 className={styles.title}>@radish/ui 组件库</h1>
        <p className={styles.subtitle}>Radish WebOS 通用组件库 · 基于 React + CSS Modules</p>

        {/* 分类导航 */}
        <div className={styles.navBar}>
          {sections.map(section => (
            <button
              key={section.id}
              className={`${styles.navItem} ${activeSection === section.id ? styles.active : ''}`}
              onClick={() => setActiveSection(section.id)}
            >
              {section.name}
            </button>
          ))}
        </div>

        {/* Icon 组件展示 */}
        {shouldShow('basic') && (
          <section className={styles.section}>
            <h2 className={styles.sectionTitle}>Icon 图标组件</h2>
            <p className={styles.description}>
              基于 @iconify/react 封装，支持海量图标集
            </p>
            <GlassPanel className={styles.demoPanel}>
              <div className={styles.iconGrid}>
                <div className={styles.iconItem}>
                  <Icon icon="mdi:home" size={32} />
                  <span>home</span>
                </div>
                <div className={styles.iconItem}>
                  <Icon icon="mdi:account" size={32} />
                  <span>account</span>
                </div>
                <div className={styles.iconItem}>
                  <Icon icon="mdi:settings" size={32} />
                  <span>settings</span>
                </div>
                <div className={styles.iconItem}>
                  <Icon icon="mdi:heart" size={32} color="#e91e63" />
                  <span>heart</span>
                </div>
                <div className={styles.iconItem}>
                  <Icon icon="mdi:star" size={32} color="#ffc107" />
                  <span>star</span>
                </div>
                <div className={styles.iconItem}>
                  <Icon icon="mdi:bell" size={32} color="#2196f3" />
                  <span>bell</span>
                </div>
                <div className={styles.iconItem}>
                  <Icon icon="mdi:folder" size={32} color="#ff9800" />
                  <span>folder</span>
                </div>
                <div className={styles.iconItem}>
                  <Icon icon="mdi:check-circle" size={32} color="#4caf50" />
                  <span>check</span>
                </div>
              </div>
            </GlassPanel>
          </section>
        )}

        {/* Button 组件展示 */}
        {shouldShow('basic') && (
          <section className={styles.section}>
            <h2 className={styles.sectionTitle}>Button 按钮组件</h2>
            <p className={styles.description}>
              支持多种变体和尺寸，可配置图标
            </p>

            <GlassPanel className={styles.demoPanel}>
              <div className={styles.buttonGroup}>
                <div>
                  <h3>Primary 主要按钮</h3>
                  <div className={styles.buttonRow}>
                    <Button variant="primary" size="small">Small</Button>
                    <Button variant="primary" size="medium">Medium</Button>
                    <Button variant="primary" size="large">Large</Button>
                  </div>
                </div>

                <div>
                  <h3>Secondary 次要按钮</h3>
                  <div className={styles.buttonRow}>
                    <Button variant="secondary" size="small">Small</Button>
                    <Button variant="secondary" size="medium">Medium</Button>
                    <Button variant="secondary" size="large">Large</Button>
                  </div>
                </div>

                <div>
                  <h3>Ghost 幽灵按钮</h3>
                  <div className={styles.buttonRow}>
                    <Button variant="ghost" size="small">Small</Button>
                    <Button variant="ghost" size="medium">Medium</Button>
                    <Button variant="ghost" size="large">Large</Button>
                  </div>
                </div>

                <div>
                  <h3>带图标的按钮</h3>
                  <div className={styles.buttonRow}>
                    <Button
                      variant="primary"
                      icon={<Icon icon="mdi:plus" size={20} />}
                    >
                      添加
                    </Button>
                    <Button
                      variant="secondary"
                      icon={<Icon icon="mdi:delete" size={20} />}
                    >
                      删除
                    </Button>
                    <Button
                      variant="ghost"
                      icon={<Icon icon="mdi:download" size={20} />}
                    >
                      下载
                    </Button>
                  </div>
                </div>

                <div>
                  <h3>禁用状态</h3>
                  <div className={styles.buttonRow}>
                    <Button variant="primary" disabled>Disabled</Button>
                    <Button variant="secondary" disabled>Disabled</Button>
                    <Button variant="ghost" disabled>Disabled</Button>
                  </div>
                </div>
              </div>
            </GlassPanel>
          </section>
        )}

        {/* GlassPanel 组件展示 */}
        {shouldShow('basic') && (
          <section className={styles.section}>
            <h2 className={styles.sectionTitle}>GlassPanel 毛玻璃面板</h2>
            <p className={styles.description}>
              提供不同模糊强度和背景色的毛玻璃效果
            </p>

            <div className={styles.panelGrid}>
              <GlassPanel blur="light" background="light" className={styles.demoCard}>
                <h3>Light Blur + Light BG</h3>
                <p>轻度模糊 + 浅色背景</p>
                <p>适用于需要清晰显示背景内容的场景</p>
              </GlassPanel>

              <GlassPanel blur="medium" background="light" className={styles.demoCard}>
                <h3>Medium Blur + Light BG</h3>
                <p>中度模糊 + 浅色背景</p>
                <p>默认配置，适合大多数桌面 UI</p>
              </GlassPanel>

              <GlassPanel blur="strong" background="light" className={styles.demoCard}>
                <h3>Strong Blur + Light BG</h3>
                <p>强度模糊 + 浅色背景</p>
                <p>适用于需要强调前景内容的场景</p>
              </GlassPanel>

              <GlassPanel blur="medium" background="dark" className={styles.demoCard}>
                <h3>Medium Blur + Dark BG</h3>
                <p>中度模糊 + 深色背景</p>
                <p>适合深色主题的桌面</p>
              </GlassPanel>
            </div>
          </section>
        )}

        {/* Input 组件展示 */}
        {shouldShow('form') && (
          <section className={styles.section}>
            <h2 className={styles.sectionTitle}>Input 输入框组件</h2>
            <p className={styles.description}>
              支持标签、占位符、错误提示等功能
            </p>

            <GlassPanel className={styles.demoPanel}>
              <div className={styles.formGroup}>
                <Input
                  label="用户名"
                  placeholder="请输入用户名"
                  value={inputValue}
                  onChange={(e) => setInputValue(e.target.value)}
                />
                <Input
                  label="密码"
                  type="password"
                  placeholder="请输入密码"
                />
                <Input
                  label="邮箱（错误状态）"
                  placeholder="请输入邮箱"
                  error="邮箱格式不正确"
                />
                <Input
                  label="禁用状态"
                  placeholder="不可编辑"
                  disabled
                />
              </div>
            </GlassPanel>
          </section>
        )}

        {/* Select 组件展示 */}
        {shouldShow('form') && (
          <section className={styles.section}>
            <h2 className={styles.sectionTitle}>Select 选择器组件</h2>
            <p className={styles.description}>
              下拉选择器，支持单选
            </p>

            <GlassPanel className={styles.demoPanel}>
              <div className={styles.formGroup}>
                <Select
                  label="选择分类"
                  placeholder="请选择"
                  value={selectValue}
                  onChange={(e) => setSelectValue(e.target.value)}
                  options={[
                    { value: 'tech', label: '技术' },
                    { value: 'life', label: '生活' },
                    { value: 'game', label: '游戏' },
                    { value: 'other', label: '其他' }
                  ]}
                />
                <Select
                  label="禁用状态"
                  placeholder="不可选择"
                  disabled
                  options={[]}
                />
              </div>
            </GlassPanel>
          </section>
        )}

        {/* ContextMenu 组件展示 */}
        {shouldShow('basic') && (
          <section className={styles.section}>
            <h2 className={styles.sectionTitle}>ContextMenu 右键菜单组件</h2>
            <p className={styles.description}>
              支持多级菜单、图标、分隔线和禁用状态
            </p>

            <GlassPanel className={styles.demoPanel}>
              <div className={styles.contextMenuDemo}>
                <div>
                  <h3>基础右键菜单</h3>
                  <ContextMenu
                    items={[
                      {
                        id: 'open',
                        label: '打开',
                        icon: <Icon icon="mdi:folder-open" size={18} />,
                        onClick: () => toast.info('打开')
                      },
                      {
                        id: 'edit',
                        label: '编辑',
                        icon: <Icon icon="mdi:pencil" size={18} />,
                        onClick: () => toast.info('编辑')
                      },
                      {
                        id: 'divider-1',
                        label: '',
                        divider: true
                      },
                      {
                        id: 'delete',
                        label: '删除',
                        icon: <Icon icon="mdi:delete" size={18} color="#e91e63" />,
                        onClick: () => toast.error('删除')
                      }
                    ]}
                  >
                    <div className={styles.contextMenuTarget}>
                      右键点击这里
                    </div>
                  </ContextMenu>
                </div>

                <div>
                  <h3>带子菜单的右键菜单</h3>
                  <ContextMenu
                    items={[
                      {
                        id: 'new',
                        label: '新建',
                        icon: <Icon icon="mdi:plus" size={18} />,
                        children: [
                          {
                            id: 'new-folder',
                            label: '文件夹',
                            icon: <Icon icon="mdi:folder" size={18} />,
                            onClick: () => toast.success('新建文件夹')
                          },
                          {
                            id: 'new-file',
                            label: '文件',
                            icon: <Icon icon="mdi:file" size={18} />,
                            onClick: () => toast.success('新建文件')
                          }
                        ]
                      },
                      {
                        id: 'sort',
                        label: '排序',
                        icon: <Icon icon="mdi:sort" size={18} />,
                        children: [
                          {
                            id: 'sort-name',
                            label: '按名称',
                            onClick: () => toast.info('按名称排序')
                          },
                          {
                            id: 'sort-date',
                            label: '按日期',
                            onClick: () => toast.info('按日期排序')
                          }
                        ]
                      }
                    ]}
                  >
                    <div className={styles.contextMenuTarget}>
                      右键点击查看子菜单
                    </div>
                  </ContextMenu>
                </div>
              </div>
            </GlassPanel>
          </section>
        )}

        {/* Modal & ConfirmDialog 组件展示 */}
        {shouldShow('feedback') && (
          <section className={styles.section}>
            <h2 className={styles.sectionTitle}>Modal & ConfirmDialog 弹窗组件</h2>
            <p className={styles.description}>
              模态框和确认对话框
            </p>

            <GlassPanel className={styles.demoPanel}>
              <div className={styles.buttonRow}>
                <Button variant="primary" onClick={() => setModalOpen(true)}>
                  打开 Modal
                </Button>
                <Button variant="secondary" onClick={() => setConfirmOpen(true)}>
                  打开确认对话框
                </Button>
              </div>
            </GlassPanel>

            <Modal
              isOpen={modalOpen}
              onClose={() => setModalOpen(false)}
              title="Modal 示例"
            >
              <div style={{ padding: '16px' }}>
                <p>这是一个模态框示例。</p>
                <p>可以在这里放置任何内容。</p>
                <div style={{ marginTop: '16px', display: 'flex', gap: '8px', justifyContent: 'flex-end' }}>
                  <Button variant="secondary" onClick={() => setModalOpen(false)}>取消</Button>
                  <Button variant="primary" onClick={() => setModalOpen(false)}>确定</Button>
                </div>
              </div>
            </Modal>

            <ConfirmDialog
              isOpen={confirmOpen}
              title="确认操作"
              message="确定要执行此操作吗？此操作不可撤销。"
              onCancel={() => setConfirmOpen(false)}
              onConfirm={() => {
                setConfirmOpen(false);
                toast.success('操作已确认');
              }}
            />
          </section>
        )}

        {/* Toast 组件展示 */}
        {shouldShow('feedback') && (
          <section className={styles.section}>
            <h2 className={styles.sectionTitle}>Toast 消息提示组件</h2>
            <p className={styles.description}>
              轻量级的消息提示，支持多种类型
            </p>

            <GlassPanel className={styles.demoPanel}>
              <div className={styles.buttonRow}>
                <Button variant="primary" onClick={() => toast.success('操作成功！')}>
                  成功提示
                </Button>
                <Button variant="secondary" onClick={() => toast.error('操作失败！')}>
                  错误提示
                </Button>
                <Button variant="ghost" onClick={() => toast.warning('请注意！')}>
                  警告提示
                </Button>
                <Button variant="ghost" onClick={() => toast.info('这是一条信息')}>
                  信息提示
                </Button>
              </div>
            </GlassPanel>
          </section>
        )}

        {/* ExperienceBar 组件展示 */}
        {shouldShow('data') && (
          <section className={styles.section}>
            <h2 className={styles.sectionTitle}>ExperienceBar 经验条组件</h2>
            <p className={styles.description}>
              展示用户等级和经验值进度
            </p>

            <GlassPanel className={styles.demoPanel}>
              <div className={styles.expBarDemo}>
                <div>
                  <h3>大尺寸</h3>
                  <ExperienceBar
                    data={mockExperienceData}
                    size="large"
                    showLevel={true}
                    showProgress={true}
                    showTooltip={true}
                    animated={true}
                  />
                </div>
                <div>
                  <h3>中尺寸</h3>
                  <ExperienceBar
                    data={mockExperienceData}
                    size="medium"
                    showLevel={true}
                    showProgress={true}
                    animated={true}
                  />
                </div>
                <div>
                  <h3>小尺寸</h3>
                  <ExperienceBar
                    data={mockExperienceData}
                    size="small"
                    showLevel={true}
                    showProgress={false}
                    animated={true}
                  />
                </div>
              </div>
            </GlassPanel>
          </section>
        )}

        {/* Skeleton 组件展示 */}
        {shouldShow('data') && (
          <section className={styles.section}>
            <h2 className={styles.sectionTitle}>Skeleton 骨架屏组件</h2>
            <p className={styles.description}>
              加载状态占位符，提升用户体验
            </p>

            <GlassPanel className={styles.demoPanel}>
              <div className={styles.skeletonDemo}>
                <div>
                  <h3>表格骨架屏</h3>
                  <TableSkeleton rows={3} columns={4} />
                </div>
                <div>
                  <h3>简单骨架屏</h3>
                  <SimpleSkeleton lines={3} />
                </div>
                <div>
                  <h3>卡片骨架屏</h3>
                  <CardSkeleton />
                </div>
              </div>
            </GlassPanel>
          </section>
        )}

        {/* Charts 组件展示 */}
        {shouldShow('charts') && (
          <section className={styles.section}>
            <h2 className={styles.sectionTitle}>Charts 图表组件</h2>
            <p className={styles.description}>
              基于 Recharts 封装的图表组件
            </p>

            <div className={styles.chartGrid}>
              <GlassPanel className={styles.chartCard}>
                <h3>LineChart 折线图</h3>
                <LineChart
                  data={lineChartData}
                  lines={[
                    { dataKey: 'value', name: '访问量', color: '#667eea', strokeWidth: 2 }
                  ]}
                  xAxisKey="date"
                  height={200}
                  showGrid={true}
                  showLegend={true}
                />
              </GlassPanel>

              <GlassPanel className={styles.chartCard}>
                <h3>BarChart 柱状图</h3>
                <BarChart
                  data={barChartData}
                  bars={[
                    { dataKey: 'value', name: '数量', color: '#764ba2' }
                  ]}
                  xAxisKey="name"
                  height={200}
                  showGrid={true}
                  showLegend={true}
                />
              </GlassPanel>

              <GlassPanel className={styles.chartCard}>
                <h3>PieChart 饼图</h3>
                <PieChart
                  data={pieChartData}
                  height={200}
                  showLegend={true}
                  innerRadius={0}
                  outerRadius={70}
                  showLabel={true}
                />
              </GlassPanel>

              <GlassPanel className={styles.chartCard}>
                <h3>AreaChart 面积图</h3>
                <AreaChart
                  data={areaChartData}
                  areas={[
                    { dataKey: 'users', name: '用户', color: '#667eea', fillOpacity: 0.3 },
                    { dataKey: 'posts', name: '帖子', color: '#f093fb', fillOpacity: 0.3 }
                  ]}
                  xAxisKey="date"
                  height={200}
                  showGrid={true}
                  showLegend={true}
                />
              </GlassPanel>
            </div>
          </section>
        )}

        {/* 组合示例 */}
        {shouldShow('basic') && (
          <section className={styles.section}>
            <h2 className={styles.sectionTitle}>组合示例</h2>
            <p className={styles.description}>
              组件组合使用的示例
            </p>

            <GlassPanel blur="medium" className={styles.demoPanel}>
              <div className={styles.cardExample}>
                <div className={styles.cardHeader}>
                  <Icon icon="mdi:bell-ring" size={32} color="#667eea" />
                  <div>
                    <h3>系统通知</h3>
                    <p>您有 3 条未读消息</p>
                  </div>
                </div>
                <div className={styles.cardContent}>
                  <div className={styles.notification}>
                    <Icon icon="mdi:account-circle" size={24} />
                    <span>用户 Alice 关注了你</span>
                    <span className={styles.time}>2分钟前</span>
                  </div>
                  <div className={styles.notification}>
                    <Icon icon="mdi:heart" size={24} color="#e91e63" />
                    <span>你的帖子获得了 10 个赞</span>
                    <span className={styles.time}>5分钟前</span>
                  </div>
                  <div className={styles.notification}>
                    <Icon icon="mdi:comment" size={24} color="#2196f3" />
                    <span>Bob 评论了你的帖子</span>
                    <span className={styles.time}>10分钟前</span>
                  </div>
                </div>
                <div className={styles.cardFooter}>
                  <Button variant="primary" size="small">
                    全部已读
                  </Button>
                  <Button variant="ghost" size="small">
                    查看全部
                  </Button>
                </div>
              </div>
            </GlassPanel>
          </section>
        )}
      </div>
      <Toast />
    </div>
  );
};
