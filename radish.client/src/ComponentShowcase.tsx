import { Icon } from './shared/ui/base/Icon';
import { Button } from './shared/ui/base/Button';
import { GlassPanel } from './shared/ui/desktop/GlassPanel';
import { ContextMenu } from './shared/ui/base/ContextMenu';
import styles from './ComponentShowcase.module.css';

/**
 * 组件展示页面
 *
 * 用于预览和测试所有基础组件
 */
export const ComponentShowcase = () => {
  return (
    <div className={styles.showcase}>
      <div className={styles.container}>
        <h1 className={styles.title}>Radish WebOS 组件库</h1>
        <p className={styles.subtitle}>基础组件预览 · CSS Modules 实现</p>

        {/* Icon 组件展示 */}
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
            </div>
          </GlassPanel>
        </section>

        {/* Button 组件展示 */}
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

        {/* GlassPanel 组件展示 */}
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

        {/* ContextMenu 组件展示 */}
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
                      onClick: () => alert('打开')
                    },
                    {
                      id: 'edit',
                      label: '编辑',
                      icon: <Icon icon="mdi:pencil" size={18} />,
                      onClick: () => alert('编辑')
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
                      onClick: () => alert('删除')
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
                          onClick: () => alert('新建文件夹')
                        },
                        {
                          id: 'new-file',
                          label: '文件',
                          icon: <Icon icon="mdi:file" size={18} />,
                          onClick: () => alert('新建文件')
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
                          onClick: () => alert('按名称排序')
                        },
                        {
                          id: 'sort-date',
                          label: '按日期',
                          onClick: () => alert('按日期排序')
                        },
                        {
                          id: 'sort-size',
                          label: '按大小',
                          onClick: () => alert('按大小排序')
                        }
                      ]
                    },
                    {
                      id: 'divider-1',
                      label: '',
                      divider: true
                    },
                    {
                      id: 'refresh',
                      label: '刷新',
                      icon: <Icon icon="mdi:refresh" size={18} />,
                      onClick: () => alert('刷新')
                    }
                  ]}
                >
                  <div className={styles.contextMenuTarget}>
                    右键点击查看子菜单
                  </div>
                </ContextMenu>
              </div>

              <div>
                <h3>带禁用项的右键菜单</h3>
                <ContextMenu
                  items={[
                    {
                      id: 'copy',
                      label: '复制',
                      icon: <Icon icon="mdi:content-copy" size={18} />,
                      onClick: () => alert('复制')
                    },
                    {
                      id: 'paste',
                      label: '粘贴',
                      icon: <Icon icon="mdi:content-paste" size={18} />,
                      disabled: true,
                      onClick: () => alert('粘贴')
                    },
                    {
                      id: 'divider-1',
                      label: '',
                      divider: true
                    },
                    {
                      id: 'properties',
                      label: '属性',
                      icon: <Icon icon="mdi:information" size={18} />,
                      onClick: () => alert('属性')
                    }
                  ]}
                >
                  <div className={styles.contextMenuTarget}>
                    右键点击（粘贴项已禁用）
                  </div>
                </ContextMenu>
              </div>
            </div>
          </GlassPanel>
        </section>

        {/* 组合示例 */}
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
      </div>
    </div>
  );
};
