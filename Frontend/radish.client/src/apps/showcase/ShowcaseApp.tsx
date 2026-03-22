import { useState } from 'react';
import { Icon } from '@radish/ui/icon';
import { Button } from '@radish/ui/button';
import { GlassPanel } from '@radish/ui/glass-panel';
import { ContextMenu } from '@radish/ui/context-menu';
import { Input } from '@radish/ui/input';
import { Select } from '@radish/ui/select';
import { Modal } from '@radish/ui/modal';
import { ConfirmDialog } from '@radish/ui/confirm-dialog';
import { ToastContainer, toast } from '@radish/ui/toast';
import { ExperienceBar, type ExperienceData } from '@radish/ui/experience-bar';
import { TableSkeleton, SimpleSkeleton, CardSkeleton } from '@radish/ui/skeleton';
import { LineChart } from '@radish/ui/line-chart';
import { BarChart } from '@radish/ui/bar-chart';
import { PieChart } from '@radish/ui/pie-chart';
import { AreaChart } from '@radish/ui/area-chart';
import { useTheme } from '@/theme/useTheme';
import styles from './ShowcaseApp.module.css';

type SectionId = 'overview' | 'basic' | 'form' | 'feedback' | 'data' | 'charts';

const sections: Array<{ id: SectionId; name: string }> = [
  { id: 'overview', name: '概览' },
  { id: 'basic', name: '基础组件' },
  { id: 'form', name: '表单组件' },
  { id: 'feedback', name: '反馈组件' },
  { id: 'data', name: '数据展示' },
  { id: 'charts', name: '图表组件' }
];

const mockExperienceData: ExperienceData = {
  voUserId: 1024,
  voCurrentLevel: 12,
  voCurrentLevelName: '青瓷旅者',
  voCurrentExp: 3680,
  voTotalExp: 21540,
  voNextLevelExp: 4200,
  voExpToNextLevel: 520,
  voNextLevelName: '云山笔客',
  voLevelProgress: 0.876,
  voThemeColor: '#587786',
  voRank: 18,
  voExpFrozen: false
};

const lineChartData = [
  { day: '周一', value: 128 },
  { day: '周二', value: 164 },
  { day: '周三', value: 151 },
  { day: '周四', value: 212 },
  { day: '周五', value: 238 },
  { day: '周六', value: 286 },
  { day: '周日', value: 244 }
];

const barChartData = [
  { name: '主题切换', value: 38 },
  { name: '弹窗', value: 24 },
  { name: '表单', value: 31 },
  { name: '消息', value: 46 }
];

const pieChartData = [
  { name: '内容区', value: 40, color: '#587786' },
  { name: '交互反馈', value: 25, color: '#4f8e77' },
  { name: '表单录入', value: 20, color: '#b24057' },
  { name: '图表展示', value: 15, color: '#8b725d' }
];

const areaChartData = [
  { day: '周一', users: 120, interactions: 58 },
  { day: '周二', users: 144, interactions: 72 },
  { day: '周三', users: 132, interactions: 68 },
  { day: '周四', users: 166, interactions: 80 },
  { day: '周五', users: 188, interactions: 94 },
  { day: '周六', users: 204, interactions: 110 },
  { day: '周日', users: 192, interactions: 98 }
];

/**
 * 组件库展示应用
 *
 * 面向当前 WebOS 主题与共享组件的维护中预览页。
 */
export const ShowcaseApp = () => {
  const { currentTheme } = useTheme();
  const [activeSection, setActiveSection] = useState<SectionId>('overview');
  const [inputValue, setInputValue] = useState('');
  const [selectValue, setSelectValue] = useState('');
  const [modalOpen, setModalOpen] = useState(false);
  const [confirmOpen, setConfirmOpen] = useState(false);

  const overviewCards = [
    {
      title: '主题同步',
      value: currentTheme.label,
      description: '展示页和共享组件统一读取 WebOS token，不再单独维护一套白底配色。'
    },
    {
      title: '加载方式',
      value: '按分类挂载',
      description: '默认先看概览，图表与浮层只在切到对应分区后再加载。'
    },
    {
      title: '共享范围',
      value: '@radish/ui',
      description: '当前面向 radish.client 与 radish.console 的基础组件和图表封装。'
    }
  ];

  const usageNotes = [
    '优先展示项目里仍在维护和真实使用的组件，去掉泛用型旧 Demo 感。',
    '图表容器加入尺寸守卫，避免窗口初次布局时 Recharts 重复报警。',
    '共享组件样式改为基于主题 token，切换 default / guofeng 会同步变化。'
  ];

  const shouldShow = (section: SectionId) => activeSection === section;

  return (
    <div className={styles.showcase}>
      <div className={styles.container}>
        <header className={styles.hero}>
          <div className={styles.heroMain}>
            <span className={styles.eyebrow}>Shared UI Workspace</span>
            <h1 className={styles.title}>Radish UI 组件库</h1>
            <p className={styles.subtitle}>
              面向当前 WebOS 的共享组件与图表封装预览页，跟随桌面主题切换，聚焦仍在维护的实际组件。
            </p>
            <div className={styles.heroBadges}>
              <span className={styles.badge}>当前主题 · {currentTheme.label}</span>
              <span className={styles.badge}>共享包 · @radish/ui</span>
              <span className={styles.badge}>窗口内预览 · WebOS</span>
            </div>
          </div>

          <GlassPanel className={styles.heroPanel}>
            <h2 className={styles.heroPanelTitle}>本页定位</h2>
            <p className={styles.heroPanelText}>
              这里不再是一次性堆满所有组件的旧式样板页，而是一个随主题变化、按类别查看、可直接观察共享组件状态的维护面板。
            </p>
            <div className={styles.heroPanelMeta}>
              <div className={styles.metaItem}>
                <span className={styles.metaLabel}>维护目标</span>
                <strong className={styles.metaValue}>统一视觉基线</strong>
              </div>
              <div className={styles.metaItem}>
                <span className={styles.metaLabel}>当前模式</span>
                <strong className={styles.metaValue}>分类加载</strong>
              </div>
            </div>
          </GlassPanel>
        </header>

        <nav className={styles.navBar} aria-label="组件分类">
          {sections.map(section => (
            <button
              key={section.id}
              type="button"
              className={`${styles.navItem} ${activeSection === section.id ? styles.active : ''}`}
              onClick={() => setActiveSection(section.id)}
            >
              {section.name}
            </button>
          ))}
        </nav>

        {shouldShow('overview') && (
          <section className={styles.section}>
            <div className={styles.sectionHeading}>
              <h2 className={styles.sectionTitle}>概览</h2>
              <p className={styles.description}>先看当前组件库的维护状态、主题接入和推荐浏览路径。</p>
            </div>

            <div className={styles.overviewGrid}>
              {overviewCards.map(card => (
                <GlassPanel key={card.title} className={styles.overviewCard}>
                  <span className={styles.overviewLabel}>{card.title}</span>
                  <strong className={styles.overviewValue}>{card.value}</strong>
                  <p className={styles.overviewText}>{card.description}</p>
                </GlassPanel>
              ))}
            </div>

            <GlassPanel className={styles.notePanel}>
              <h3 className={styles.noteTitle}>维护说明</h3>
              <div className={styles.noteList}>
                {usageNotes.map(note => (
                  <div key={note} className={styles.noteItem}>
                    <span className={styles.noteDot} />
                    <span>{note}</span>
                  </div>
                ))}
              </div>
            </GlassPanel>
          </section>
        )}

        {shouldShow('basic') && (
          <>
            <section className={styles.section}>
              <div className={styles.sectionHeading}>
                <h2 className={styles.sectionTitle}>Icon 图标组件</h2>
                <p className={styles.description}>优先展示当前桌面和业务页中真实使用的导航与状态图标。</p>
              </div>

              <GlassPanel className={styles.demoPanel}>
                <div className={styles.iconGrid}>
                  {[
                    ['mdi:hand-wave', 'welcome'],
                    ['mdi:view-grid-plus', 'showcase'],
                    ['mdi:notebook-edit-outline', 'document'],
                    ['mdi:forum', 'forum'],
                    ['mdi:message-text', 'chat'],
                    ['mdi:account', 'profile'],
                    ['mdi:bell', 'notification'],
                    ['mdi:wallet', 'wallet']
                  ].map(([icon, label]) => (
                    <div key={label} className={styles.iconItem}>
                      <Icon icon={icon} size={28} />
                      <span>{label}</span>
                    </div>
                  ))}
                </div>
              </GlassPanel>
            </section>

            <section className={styles.section}>
              <div className={styles.sectionHeading}>
                <h2 className={styles.sectionTitle}>Button 与 GlassPanel</h2>
                <p className={styles.description}>基础操作与容器现在都跟随主题 token 变化。</p>
              </div>

              <div className={styles.splitGrid}>
                <GlassPanel className={styles.demoPanel}>
                  <div className={styles.buttonGroup}>
                    <div>
                      <h3 className={styles.blockTitle}>动作按钮</h3>
                      <div className={styles.buttonRow}>
                        <Button variant="primary" size="medium">发布内容</Button>
                        <Button variant="secondary" size="medium">保存草稿</Button>
                        <Button variant="ghost" size="medium">查看详情</Button>
                        <Button variant="danger" size="medium">移除条目</Button>
                      </div>
                    </div>

                    <div>
                      <h3 className={styles.blockTitle}>带图标的按钮</h3>
                      <div className={styles.buttonRow}>
                        <Button variant="primary" icon={<Icon icon="mdi:plus" size={18} />}>新建窗口</Button>
                        <Button variant="secondary" icon={<Icon icon="mdi:content-save-outline" size={18} />}>保存</Button>
                        <Button variant="ghost" icon={<Icon icon="mdi:theme-light-dark" size={18} />}>切换主题</Button>
                      </div>
                    </div>
                  </div>
                </GlassPanel>

                <div className={styles.panelGrid}>
                  <GlassPanel blur="light" background="light" className={styles.demoCard}>
                    <h3 className={styles.blockTitle}>浅层容器</h3>
                    <p>适合列表、辅助面板和内容分组。</p>
                  </GlassPanel>

                  <GlassPanel blur="medium" background="light" className={styles.demoCard}>
                    <h3 className={styles.blockTitle}>标准容器</h3>
                    <p>默认展示面板，强调当前内容但不割裂背景。</p>
                  </GlassPanel>

                  <GlassPanel blur="medium" background="dark" className={styles.demoCard}>
                    <h3 className={styles.blockTitle}>深色容器</h3>
                    <p>适合浮层、图像底图和需要压暗背景的局部场景。</p>
                  </GlassPanel>
                </div>
              </div>
            </section>

            <section className={styles.section}>
              <div className={styles.sectionHeading}>
                <h2 className={styles.sectionTitle}>ContextMenu 右键菜单</h2>
                <p className={styles.description}>用于桌面、附件、文档等需要就地操作的场景。</p>
              </div>

              <GlassPanel className={styles.demoPanel}>
                <div className={styles.contextMenuDemo}>
                  <ContextMenu
                    items={[
                      {
                        id: 'open',
                        label: '打开',
                        icon: <Icon icon="mdi:open-in-new" size={18} />,
                        onClick: () => toast.info('打开当前条目')
                      },
                      {
                        id: 'pin',
                        label: '固定到桌面',
                        icon: <Icon icon="mdi:pin-outline" size={18} />,
                        onClick: () => toast.success('已固定')
                      },
                      {
                        id: 'divider-1',
                        label: '',
                        divider: true
                      },
                      {
                        id: 'archive',
                        label: '归档',
                        icon: <Icon icon="mdi:archive-outline" size={18} />,
                        onClick: () => toast.warning('已加入归档队列')
                      }
                    ]}
                  >
                    <div className={styles.contextMenuTarget}>右键查看基础菜单</div>
                  </ContextMenu>

                  <ContextMenu
                    items={[
                      {
                        id: 'sort',
                        label: '排序方式',
                        icon: <Icon icon="mdi:sort" size={18} />,
                        children: [
                          {
                            id: 'sort-recent',
                            label: '按最近访问',
                            onClick: () => toast.info('按最近访问排序')
                          },
                          {
                            id: 'sort-usage',
                            label: '按使用频率',
                            onClick: () => toast.info('按使用频率排序')
                          }
                        ]
                      },
                      {
                        id: 'theme',
                        label: '主题偏好',
                        icon: <Icon icon="mdi:palette-outline" size={18} />,
                        children: [
                          {
                            id: 'theme-default',
                            label: '简雅',
                            onClick: () => toast.success('切换到简雅主题')
                          },
                          {
                            id: 'theme-guofeng',
                            label: '国风',
                            onClick: () => toast.success('切换到国风主题')
                          }
                        ]
                      }
                    ]}
                  >
                    <div className={styles.contextMenuTarget}>右键查看带子菜单版本</div>
                  </ContextMenu>
                </div>
              </GlassPanel>
            </section>
          </>
        )}

        {shouldShow('form') && (
          <section className={styles.section}>
            <div className={styles.sectionHeading}>
              <h2 className={styles.sectionTitle}>表单组件</h2>
              <p className={styles.description}>输入、选择和校验态统一使用主题边框与焦点高亮。</p>
            </div>

            <div className={styles.splitGrid}>
              <GlassPanel className={styles.demoPanel}>
                <div className={styles.formGroup}>
                  <Input
                    label="帖子标题"
                    placeholder="输入标题，最多 40 字"
                    value={inputValue}
                    onChange={(e) => setInputValue(e.target.value)}
                    helperText="示例：国风主题改造进度同步"
                  />
                  <Input
                    label="访问口令"
                    type="password"
                    placeholder="请输入访问口令"
                  />
                  <Input
                    label="邮箱（错误态）"
                    placeholder="请输入邮箱地址"
                    error="邮箱格式不正确"
                  />
                </div>
              </GlassPanel>

              <GlassPanel className={styles.demoPanel}>
                <div className={styles.formGroup}>
                  <Select
                    label="组件分类"
                    placeholder="请选择组件分类"
                    value={selectValue}
                    onChange={(e) => setSelectValue(e.target.value)}
                    options={[
                      { value: 'basic', label: '基础交互' },
                      { value: 'form', label: '表单录入' },
                      { value: 'feedback', label: '反馈浮层' },
                      { value: 'charts', label: '图表展示' }
                    ]}
                  />
                  <Select
                    label="禁用态"
                    placeholder="当前不可选择"
                    disabled
                    options={[]}
                  />
                </div>
              </GlassPanel>
            </div>
          </section>
        )}

        {shouldShow('feedback') && (
          <>
            <section className={styles.section}>
              <div className={styles.sectionHeading}>
                <h2 className={styles.sectionTitle}>Modal 与 ConfirmDialog</h2>
                <p className={styles.description}>用于确认操作、补充编辑和临时工作流。</p>
              </div>

              <GlassPanel className={styles.demoPanel}>
                <div className={styles.buttonRow}>
                  <Button variant="primary" onClick={() => setModalOpen(true)}>打开编辑弹窗</Button>
                  <Button variant="secondary" onClick={() => setConfirmOpen(true)}>打开确认对话框</Button>
                </div>
              </GlassPanel>

              <Modal
                isOpen={modalOpen}
                onClose={() => setModalOpen(false)}
                title="组件说明补充"
              >
                <div className={styles.modalBody}>
                  <p>这里用于展示共享组件的补充说明、参数约束和使用建议。</p>
                  <p>当前弹窗也已切换到统一主题变量，不再维持旧的固定白底样式。</p>
                  <div className={styles.modalActions}>
                    <Button variant="secondary" onClick={() => setModalOpen(false)}>取消</Button>
                    <Button variant="primary" onClick={() => setModalOpen(false)}>确认</Button>
                  </div>
                </div>
              </Modal>

              <ConfirmDialog
                isOpen={confirmOpen}
                title="确认更新展示状态"
                message="确认后会保留当前筛选分类，并重新计算展示页可见组件状态。"
                onCancel={() => setConfirmOpen(false)}
                onConfirm={() => {
                  setConfirmOpen(false);
                  toast.success('展示状态已更新');
                }}
              />
            </section>

            <section className={styles.section}>
              <div className={styles.sectionHeading}>
                <h2 className={styles.sectionTitle}>Toast 消息提示</h2>
                <p className={styles.description}>用于轻量反馈，不打断当前操作流。</p>
              </div>

              <GlassPanel className={styles.demoPanel}>
                <div className={styles.buttonRow}>
                  <Button variant="primary" onClick={() => toast.success('发布成功，已同步到桌面状态')}>成功提示</Button>
                  <Button variant="secondary" onClick={() => toast.error('保存失败，请稍后重试')}>错误提示</Button>
                  <Button variant="ghost" onClick={() => toast.warning('还有未完成字段')}>警告提示</Button>
                  <Button variant="ghost" onClick={() => toast.info('当前已切换到概览模式')}>信息提示</Button>
                </div>
              </GlassPanel>
            </section>
          </>
        )}

        {shouldShow('data') && (
          <>
            <section className={styles.section}>
              <div className={styles.sectionHeading}>
                <h2 className={styles.sectionTitle}>ExperienceBar 经验条</h2>
                <p className={styles.description}>用于等级、成长与进度类展示，支持主题色和悬停信息。</p>
              </div>

              <GlassPanel className={styles.demoPanel}>
                <div className={styles.expBarDemo}>
                  <div>
                    <h3 className={styles.blockTitle}>Large</h3>
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
                    <h3 className={styles.blockTitle}>Medium</h3>
                    <ExperienceBar
                      data={mockExperienceData}
                      size="medium"
                      showLevel={true}
                      showProgress={true}
                      animated={true}
                    />
                  </div>
                  <div>
                    <h3 className={styles.blockTitle}>Small</h3>
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

            <section className={styles.section}>
              <div className={styles.sectionHeading}>
                <h2 className={styles.sectionTitle}>Skeleton 骨架屏</h2>
                <p className={styles.description}>用于加载中的内容占位，减少白屏和布局跳动。</p>
              </div>

              <GlassPanel className={styles.demoPanel}>
                <div className={styles.skeletonDemo}>
                  <div>
                    <h3 className={styles.blockTitle}>表格列表</h3>
                    <TableSkeleton rows={3} columns={4} />
                  </div>
                  <div>
                    <h3 className={styles.blockTitle}>文本块</h3>
                    <SimpleSkeleton lines={3} />
                  </div>
                  <div>
                    <h3 className={styles.blockTitle}>卡片集</h3>
                    <CardSkeleton />
                  </div>
                </div>
              </GlassPanel>
            </section>
          </>
        )}

        {shouldShow('charts') && (
          <section className={styles.section}>
            <div className={styles.sectionHeading}>
              <h2 className={styles.sectionTitle}>Charts 图表组件</h2>
              <p className={styles.description}>图表容器已增加尺寸守卫，适配桌面窗口初次布局和主题配色。</p>
            </div>

            <div className={styles.chartGrid}>
              <GlassPanel className={styles.chartCard}>
                <h3 className={styles.blockTitle}>访问趋势</h3>
                <LineChart
                  data={lineChartData}
                  lines={[
                    { dataKey: 'value', name: '访问量', color: 'var(--theme-brand-primary, #587786)', strokeWidth: 2.4 }
                  ]}
                  xAxisKey="day"
                  height={240}
                  showGrid={true}
                  showLegend={true}
                />
              </GlassPanel>

              <GlassPanel className={styles.chartCard}>
                <h3 className={styles.blockTitle}>组件使用频次</h3>
                <BarChart
                  data={barChartData}
                  bars={[
                    { dataKey: 'value', name: '次数', color: 'var(--theme-accent-earth, #8b725d)' }
                  ]}
                  xAxisKey="name"
                  height={240}
                  showGrid={true}
                  showLegend={true}
                />
              </GlassPanel>

              <GlassPanel className={styles.chartCard}>
                <h3 className={styles.blockTitle}>展示内容构成</h3>
                <PieChart
                  data={pieChartData}
                  height={240}
                  showLegend={true}
                  innerRadius={42}
                  outerRadius={78}
                  showLabel={false}
                />
              </GlassPanel>

              <GlassPanel className={styles.chartCard}>
                <h3 className={styles.blockTitle}>活跃与交互</h3>
                <AreaChart
                  data={areaChartData}
                  areas={[
                    { dataKey: 'users', name: '活跃用户', color: 'var(--theme-brand-primary, #587786)', fillOpacity: 0.28 },
                    { dataKey: 'interactions', name: '交互次数', color: 'var(--theme-accent-jade, #5d8d7d)', fillOpacity: 0.24 }
                  ]}
                  xAxisKey="day"
                  height={240}
                  showGrid={true}
                  showLegend={true}
                />
              </GlassPanel>
            </div>
          </section>
        )}
      </div>
      <ToastContainer />
    </div>
  );
};
