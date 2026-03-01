# 表情包系统 - UI 视觉与交互规范

> Radish 表情包系统前端 UI 规范，包含尺寸、颜色、动效、交互细节
>
> **版本**: v26.2.1
>
> **最后更新**: 2026.02.27
>
> **关联文档**：
> [系统总览与数据模型](./emoji-sticker-system.md) ·
> [Console 管理后台实现](./emoji-sticker-console.md)

---

## 当前落地状态（2026-02-27 夜间）

- ✅ `StickerPicker` insert 模式已接入 `MarkdownEditor` 与 `CreateCommentForm`
- ✅ `MarkdownEditor` 已统一使用 `StickerPicker`，不再保留旧独立 Emoji 面板分支
- ✅ 帖子与评论均已支持 `sticker://` 渲染（含 fallback 与降级占位）
- ✅ 贴图选择后已接入 `RecordUse` 上报链路
- ✅ `StickerPicker` 已支持分组文字标签与 `left/right` 面板对齐（评论场景使用左对齐）
- ⏳ `StickerPicker` reaction 模式与 `ReactionBar` 仍在下一阶段

---

## 一、Emoji / Sticker 显示规格

### 1.1 Unicode Emoji

| 场景 | 字号 | 行为 |
|------|------|------|
| 正文（帖子/评论 Markdown 渲染） | 继承正文字号（`1em`，约 16px） | 随正文缩放，不独立处理 |
| 评论框输入中 | 继承输入框字号（`14px`） | 原生文本行为 |
| ReactionBar 气泡 | `18px` | 固定，不随外部字号变化 |
| ReactionBar 快速选择器 | `24px` | 方便点击 |
| StickerPicker EmojiTab 网格 | `24px` | 网格单元格 `36×36px` |

### 1.2 自定义 Sticker（官方表情包图片）

| 场景 | 显示尺寸 | 图片来源 | GIF 行为 |
|------|----------|----------|----------|
| 正文内嵌（`sticker://` 渲染） | `2em × 2em`（约 32px），`vertical-align: -0.5em` | `imageUrl`（原图） | 直接播放动图 |
| StickerPicker 网格预览 | `48×48px` | `thumbnailUrl`（缩略图） | 展示静止第一帧；hover 切换至 `imageUrl` 播放动图（延迟 200ms，防止快速划过时闪烁） |
| ReactionBar 气泡 | `20×20px` | `thumbnailUrl`（缩略图） | 始终展示静止缩略图（气泡内尺寸小，动图无意义且影响性能） |
| ReactionBar 快速选择器 | `32×32px` | `thumbnailUrl`（缩略图） | 同上，静止图 |
| Console 管理后台网格 | `64×64px` | `thumbnailUrl`（缩略图） | hover 时播放动图 |
| Console 管理后台列表行 | `40×40px` | `thumbnailUrl`（缩略图） | 静止图 |
| Console BatchUploadConfirmTable | `48×48px` | 上传完成后的原图预览 | hover 时播放动图 |

### 1.3 正文内嵌 Sticker 的排版行为

```css
/* MarkdownRenderer 中 sticker:// 图片的样式 */
.stickerInline {
  display: inline-block;
  width: 2em;
  height: 2em;
  vertical-align: -0.5em;   /* 与文字基线对齐，避免图片撑高行高 */
  object-fit: contain;
  cursor: default;           /* 不显示指针，不触发灯箱 */
  margin: 0 0.1em;           /* 与前后文字的间距 */
  border-radius: 2px;        /* 轻微圆角，与 emoji 风格统一 */
}
```

**注意事项**：
- `sticker://` 图片**不触发灯箱**，需在渲染逻辑中显式阻止 click 冒泡
- 连续多个 sticker 之间无换行（inline-block 行为），与 emoji 字符表现一致
- 纯 sticker 行（一行只有 sticker 无文字）：最大高度为 `3em`（防止超大 sticker 撑开布局）

---

## 二、StickerPicker 组件规范

### 2.1 整体尺寸与布局

```
┌─────────────────────────────────────────────┐  ← 宽 360px
│ [😀] [表情包分组1图标] [表情包分组2图标] ...  │  ← Tab 栏，高 40px
├─────────────────────────────────────────────┤
│ 🔍 搜索表情...                               │  ← 搜索框，高 36px（含内边距）
├─────────────────────────────────────────────┤
│                                             │
│   网格内容区（可滚动）                        │  ← 高 280px，固定高度，内部滚动
│                                             │
└─────────────────────────────────────────────┘
                                        ↑ 总高 ~380px，宽 360px
```

Popover 定位：触发按钮的上方或下方（根据视口空间自动决定），水平方向对齐触发按钮左边缘。

### 2.2 Tab 栏

- 第一个 Tab：Unicode Emoji（图标用 `mdi:emoticon-happy-outline`）
- 后续 Tab：各表情包分组，展示分组 `CoverImageUrl`（24×24px，圆形裁剪）
- 分组过多时 Tab 栏水平滚动（隐藏滚动条，支持鼠标横向滚动）
- 当前激活 Tab 下方显示 `2px` 主题色底线

### 2.3 搜索框

- **搜索范围**：EmojiTab 时搜索 emoji 名称（英文）；StickerTab 时搜索 `Sticker.Name`（中文）
- **搜索结果**：混合展示（unicode emoji 和 sticker 各自以小标题分组），按 `UseCount` 降序排列同类结果
- **空结果提示**："未找到匹配的表情"
- **清空按钮**：搜索框内容非空时显示 `×` 清空按钮

### 2.4 EmojiTab 网格

- 每行 9 列，单元格 `36×36px`，emoji 字号 `24px`
- 分类标题（如"表情"、"手势"、"符号"）：高 `24px`，灰色小字
- 分类总数约 10 类，覆盖 Unicode 15.0 常用 emoji（约 600–800 个）
- 当前分类对应的 Tab 快速跳转：搜索框右侧显示分类图标行（点击跳转到对应分类）

### 2.5 StickerTab 网格

- 每行 6 列，单元格 `56×68px`（48px 图片 + 8px 下方留白）
- 缩略图居中，下方无文字（name 通过 tooltip 展示，hover 延迟 400ms 显示）
- GIF badge：GIF 表情的左上角展示 `GIF` 文字标签（`10px` 字号，半透明黑底白字）
- `AllowInline = false` 的表情：在"插入模式"（来自 MarkdownEditor/评论框）下显示为灰色不可选状态；在"Reaction 模式"（来自 ReactionBar）下正常可选

### 2.6 StickerPicker 的两种模式

**插入模式**（来自 MarkdownEditor / CreateCommentForm）：
- 选择 unicode emoji → 直接插入字符
- 选择 sticker → 插入 `![name](sticker://packCode/stickerCode#radish:image=...&thumbnail=...)` 语法，同时调用 `RecordUse`
- `AllowInline = false` 的 sticker 不可选，显示 tooltip："此表情仅可用于 Reaction"
- 当前代码状态：发帖、编辑帖子、评论输入框三处均已接入 insert 模式

**Reaction 模式**（来自 ReactionBar 的"更多"按钮）：
- 选择任意 emoji / sticker → 调用 `Toggle` 接口
- `AllowInline` 状态不影响 Reaction 模式下的可选性
- 当前代码状态：尚未接入（规划中）

Props 通过 `mode: 'insert' | 'reaction'` 区分。

---

## 三、ReactionBar 组件规范

### 3.1 气泡条布局

```
[😀 12]  [😢 3]  [radish_girl/happy 图标 7]  [+]
   ↑已反应（高亮）                              ↑ 添加按钮
```

- 气泡排列：水平流式，间距 `4px`
- 每个气泡：高 `28px`，圆角 `14px`（胶囊形），内边距 `0 8px`
- 气泡内容：表情（18px emoji 字符 或 20×20px 缩略图）+ 空格 + 计数（`13px`）
- **已反应高亮**：背景色使用主题 primary 色的 15% 不透明度，边框使用 primary 色
- **未反应默认**：背景色为 `rgba(0,0,0,0.06)`（暗色主题：`rgba(255,255,255,0.1)`），边框透明
- 计数为 0 时不展示该气泡（软删除后计数归零即消失）

### 3.2 折叠逻辑

- 最多展示 **8 个**气泡，超出部分折叠
- 折叠后显示 `+N` 气泡（如 `+5`），点击展开全部
- 展开后显示"收起"链接

### 3.3 添加按钮（`+`）

- 始终显示在气泡条末尾
- 未登录：点击后展示登录提示 tooltip（"登录后可添加回应"），不弹出选择器
- 已登录：hover 后延迟 `200ms` 展示 **ReactionPickerPopover**

### 3.4 ReactionPickerPopover（快速选择器）

```
┌──────────────────────────────────────┐
│  😀  😂  ❤️  👍  😢  😡  [···]      │
└──────────────────────────────────────┘
   ← 6 个常用 emoji（可配置） →   ↑ 更多
```

- 宽度：自适应内容，约 `220px`
- 高度：单行 `48px`
- 6 个常用 emoji：固定为 `😀 😂 ❤️ 👍 😢 😮`（后续可做为用户配置项）
- `···` 按钮：打开完整 StickerPicker（Reaction 模式）
- hover 离开后延迟 `300ms` 关闭（防止鼠标移动到 Popover 上时消失）

### 3.5 Reaction 计数动画

气泡计数发生变化时（`+1` 或 `-1`）：
- 数字使用 CSS `@keyframes` 做向上/向下滑出+滑入动画，时长 `150ms`
- 气泡整体做轻微缩放（`scale: 1.0 → 1.15 → 1.0`），时长 `200ms`
- 新出现的气泡（从 0 变为有人反应）从 `opacity: 0, scale: 0.8` 过渡到 `opacity: 1, scale: 1.0`

### 3.6 已登录用户的 Reaction 气泡交互

- **点击已高亮的气泡**：取消本人的 Reaction（软删除），气泡计数 -1
- **点击未高亮的气泡**：添加该 Reaction，气泡计数 +1，气泡变高亮
- **乐观更新**：点击后立即更新本地状态，后台异步发送请求；失败时回滚并提示 toast
- **防重复点击**：按钮在请求期间禁用（`loading` 状态），防止快速双击导致状态混乱

### 3.7 超过上限时的提示

当用户已对同一内容添加了 **10 种**不同 Reaction 时：
- 快速选择器和 StickerPicker 中所有未反应的表情显示禁用状态
- tooltip："你已对该内容添加了 10 种回应（上限）"
- 已反应的表情仍可点击取消

---

## 四、GIF 动图规范

### 4.1 播放策略

| 场景 | 策略 | 原因 |
|------|------|------|
| StickerPicker 网格 | 默认静止，hover 200ms 后播放 | 防止大量 GIF 同时播放导致性能问题 |
| 正文内嵌 | 始终播放 | 用户主动插入，期望看到动效 |
| ReactionBar 气泡 | 始终静止（缩略图） | 尺寸小（20px），动图无意义且分散注意力 |
| ReactionBar 快速选择器 | 始终静止（缩略图） | 同上 |
| Console 管理后台 | hover 时播放 | 管理员需要预览效果 |

### 4.2 性能控制

**StickerPicker 网格 GIF 播放**：

使用 `<img>` 的 `src` 切换（`thumbnailUrl` ↔ `imageUrl`）实现 hover 播放/暂停，而非 CSS 动画控制，避免已加载的 GIF 持续消耗 CPU：

```typescript
const [isHovered, setIsHovered] = useState(false);

<img
  src={isHovered && sticker.isAnimated ? sticker.imageUrl : sticker.thumbnailUrl}
  onMouseEnter={() => setIsHovered(true)}
  onMouseLeave={() => setIsHovered(false)}
/>
```

**正文内嵌 GIF 数量限制**：单篇帖子/评论渲染时，若 `sticker://` 图片中 GIF 数量超过 **5 个**，超出的 GIF 默认展示缩略图（静止），右下角显示 `▶` 图标，点击后才播放。避免帖子中大量 GIF 同时播放导致浏览器卡顿。

### 4.3 GIF 上传注意事项（Console 提示文案）

上传区域下方展示：
> GIF 动图上传后系统将自动提取第一帧作为缩略图。GIF 原图最大 5MB，建议帧数不超过 30 帧，时长不超过 3 秒，以确保在移动端流畅显示。

---

## 五、性能优化策略

### 5.1 StickerPicker 数据加载

- 当前实现：通过 `useStickerCatalog` 做模块级缓存（含 in-flight 去重），多入口复用同一份分组数据
- StickerPicker 组件本身不发请求，从上层接收 `stickerGroups` prop
- 切换 Tab 时无网络请求，全部本地数据
- 搜索也是纯前端过滤，无需防抖请求

后续可选优化：

- 升级为页面级或应用级 Context 统一下发，减少 Hook 调用层级

### 5.2 StickerPicker 网格虚拟化

当单个表情包分组内表情数量超过 **200 个**时，启用虚拟滚动（`react-window` 或类似方案），避免一次性渲染大量 DOM：

- 使用 `FixedSizeGrid`（固定单元格尺寸，计算简单）
- 滚动容器高度固定为 `280px`（与 Picker 设计一致）
- 低于 200 个时使用普通渲染（避免引入虚拟化的复杂性）

阈值 200 的依据：48px 单元格在 360px 容器内每行 6 列，200 个约 34 行，DOM 节点约 200 个，在现代浏览器中不构成性能问题；超过 200 个时一次性渲染 DOM 数量增长，虚拟化收益明显。

### 5.3 MarkdownRenderer stickerMap 数据传递

当前实现采用 `stickerMap` 下发：

- 帖子详情：`PostDetail` 通过 `useStickerCatalog` 获取 `stickerMap`，传给 `MarkdownRenderer`
- 评论详情：`PostDetailContentView` 获取 `stickerMap` 后传给 `CommentTree` / `CommentNode`

推荐演进（帖子列表批量场景）：

```typescript
// 页面层一次性准备 stickerMap
const { stickerMap } = useStickerCatalog();

// 下发到 MarkdownRenderer / CommentNode
<MarkdownRenderer content={content} stickerMap={stickerMap} />
```

### 5.4 ReactionBar BatchGetSummary

评论树展开时，对可见的评论批量请求 Reaction 数据（单次最多 100 条），而非每条评论单独请求：

- 评论树初次渲染：收集所有可见评论 ID，一次 `BatchGetSummary`
- 滚动加载更多评论：新增评论的 Reaction 随主评论列表接口返回（后端在评论 VO 中内嵌 Reaction 汇总），无需额外请求
- **注意**：`BatchGetSummary` 返回的 `VoIsReacted` 需传入当前用户 ID，匿名访问时全部为 `false`

### 5.5 Reaction 缓存

ReactionBar 的 `VoIsReacted` 状态在用户 Toggle 操作后**乐观更新本地状态**，避免操作后重新请求：

- Toggle 成功响应直接包含最新 `ReactionSummaryVo[]`，前端替换本地状态
- Toggle 失败时回滚到操作前状态，显示 toast 提示
- 页面级 Reaction 状态存储在 `useReactions(targetType, targetId)` Hook 中，避免 prop drilling

---

## 六、主题适配

所有表情包相关组件支持亮色/暗色主题切换，关键 CSS 变量：

| 变量 | 亮色 | 暗色 | 用途 |
|------|------|------|------|
| `--reaction-bubble-bg` | `rgba(0,0,0,0.06)` | `rgba(255,255,255,0.1)` | 未反应气泡背景 |
| `--reaction-bubble-active-bg` | `rgba(var(--primary-rgb), 0.15)` | `rgba(var(--primary-rgb), 0.25)` | 已反应气泡背景 |
| `--reaction-bubble-active-border` | `var(--primary-color)` | `var(--primary-color)` | 已反应气泡边框 |
| `--sticker-picker-bg` | `#ffffff` | `#2a2a2a` | StickerPicker 背景 |
| `--sticker-picker-tab-active` | `var(--primary-color)` | `var(--primary-color)` | Tab 激活底线颜色 |
| `--sticker-picker-hover-bg` | `rgba(0,0,0,0.06)` | `rgba(255,255,255,0.1)` | 表情 hover 背景 |

---

## 七、前端组件目录总结

```
Frontend/
├── radish.ui/src/components/
│   ├── StickerPicker/
│   │   ├── StickerPicker.tsx           # 主容器（含 Tab、搜索、网格）
│   │   ├── StickerPicker.module.css
│   │   └── index.ts
│
└── radish.client/src/
    ├── api/
    │   └── sticker.ts                  # getStickerGroups, recordStickerUse
    ├── apps/forum/hooks/
    │   └── useStickerCatalog.ts        # 分组缓存与 stickerMap 构建
    ├── apps/forum/components/
    │   ├── PublishPostModal.tsx
    │   ├── EditPostModal.tsx
    │   ├── CreateCommentForm.tsx
    │   ├── PostDetail.tsx              # MarkdownRenderer + stickerMap
    │   └── CommentNode.tsx             # 评论 sticker:// 渲染
```

---

## 八、无障碍与键盘交互（补充）

### 8.1 StickerPicker 键盘支持

- 打开 Picker 后，焦点自动落在搜索框
- `Tab / Shift+Tab`：在 Tab 栏、搜索框、网格间循环
- `↑ ↓ ← →`：在网格内移动焦点
- `Enter`：选择当前聚焦 emoji/sticker
- `Esc`：关闭 Picker 并将焦点还给触发按钮

### 8.2 ARIA 与可读文本

- Sticker 图片按钮需提供 `aria-label`（优先 `Sticker.Name`）
- `AllowInline=false` 的禁用项需提供原因文本（`aria-describedby`）
- Reaction 气泡读屏文本格式：`{emojiName}，{count} 人回应，{已选择/未选择}`
- 纯图标按钮（`+`、`···`、关闭按钮）必须有可读文本标签

### 8.3 焦点可视化

- 键盘聚焦态需有清晰外描边（不依赖 hover）
- 焦点颜色对比度满足 WCAG AA（建议至少 3:1）

---

## 九、移动端与触屏交互（补充）

### 9.1 无 hover 场景降级

- 触屏设备不依赖 hover 触发动图播放
- GIF 预览改为“点击一次播放，再次点击停止”或保持静止缩略图
- Reaction 快速选择器改为点击 `+` 触发，不使用 hover 延迟展开

### 9.2 Picker 尺寸与布局

- 小屏（宽度 < 480px）时，Picker 宽度改为 `min(360px, 92vw)`
- 内容区高度从 `280px` 降为 `240px`，避免遮挡输入框
- Tab 栏支持手势横向滑动，保留分组图标可见性

### 9.3 触控点击热区

- Emoji/sticker 可点击区域最小 `36×36px`
- Reaction 气泡点击热区最小高度 `32px`
- 与边缘距离至少 `8px`，减少误触

---

## 十、Emoji 数据源与搜索规范（补充）

### 10.1 数据源

- Unicode Emoji 数据采用固定版本快照（建议 `15.0`），避免不同环境结果不一致
- 数据文件建议放置在 `radish.ui` 内（如 `src/components/StickerPicker/data/emoji-data.ts`）
- 每条数据最少字段：`char`、`name`、`keywords[]`、`category`

### 10.2 搜索标准化

- 搜索前统一做小写化与首尾空白裁剪
- 连续空白折叠为单空格，提升英文多词匹配稳定性
- 支持按 `name` 与 `keywords` 匹配，结果按“完全前缀 > 子串 > 关键词”排序

### 10.3 版本升级策略

- 升级 Unicode 版本时，记录变更清单（新增/弃用 emoji）
- 对历史帖子中的 emoji 字符不做替换，保持原文语义
- 升级后补充最小回归：搜索、分类跳转、插入行为、Reaction 显示

---

## 十一、组件 Props 契约（补充）

### 11.1 StickerPickerProps（建议）

```typescript
type StickerPickerMode = 'insert' | 'reaction';

interface StickerPickerProps {
  groups: StickerPickerGroup[];
  onSelect: (selection: StickerPickerSelection) => void;
  mode?: StickerPickerMode;
  theme?: 'dark' | 'light';
  disabled?: boolean;
  className?: string;
  triggerTitle?: string;
  emojis?: string[];
}
```

行为约定：

- `mode='insert'`：`AllowInline=false` 项禁用并给出提示
- `mode='reaction'`：允许选择所有 sticker，不受 `AllowInline` 影响
- 组件内部不发请求，仅消费上层传入数据

### 11.2 ReactionBarProps（建议）

```typescript
interface ReactionBarProps {
  targetType: 'Post' | 'Comment' | 'ChatMessage';
  targetId: number;
  items: ReactionSummaryVo[];
  isLoggedIn: boolean;
  loading?: boolean;
  onToggle: (payload: {
    emojiType: 'unicode' | 'sticker';
    emojiValue: string;
  }) => Promise<void>;
  onOpenPicker?: () => void;
}
```

行为约定：

- 点击气泡只触发 `onToggle`，组件不直接调用 API
- 请求中禁止重复点击（loading 态）
- 失败由上层回滚 `items` 并提示错误

---

## 十二、前端回归清单（补充）

### 12.1 交互回归

- 插入模式：emoji 与 sticker 都可写入编辑器，光标位置正确
- Reaction 模式：点选后计数即时变化，失败可回滚
- 搜索：Emoji 英文关键字与 Sticker 中文名都可命中
- 禁用态：`AllowInline=false` 在插入模式不可选、Reaction 模式可选

### 12.2 兼容回归

- 桌面端 Chrome/Edge 最近两个稳定版本
- 移动端窄屏（<480px）不遮挡输入区
- 无 hover 环境下 GIF 与快速选择器交互正常

### 12.3 性能回归

- 单分组 50 / 200 / 500 项三档数据下滚动与搜索体验可接受
- 评论树批量 `BatchGetSummary` 场景下，无明显重复请求与闪烁
