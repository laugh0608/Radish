# F4-R-T01 Web 主题语义基线实现与静态门禁

> 日期：2026-08-15（Asia/Shanghai）
>
> 状态：T01 已完成；下一顺位进入 `F4-R-T02 reduced-motion 与静态退出门禁`
>
> 范围：`radish.client` 四主题语义、`@radish/ui` 共享主题公开契约、主题测试与说明文档

## 1. 结论

T01 已把 `guofeng` 灰玉品牌裁决写入正式 Client 运行时，并关闭品牌、操作与状态语义混用：

- `guofeng` 品牌、悬停、柔底和品牌实底前景固定为 `#5d6c57 / #6e736d / #e2e6de / #fffdf8`；主题预览、WebOS 壳层弱装饰与 Dock 激活面同步消费品牌语义；
- L2 `--theme-brand-hover` 直接映射显式 L1 `--rd-brand-hover`，链接与兼容 `--color-primary` 改为 action owner；
- Client 四主题 Ant Design 主操作、悬停和链接统一映射各自 action：`default / guofeng = #435c74`、`theme-dark-night = #8bb9ca`、`theme-sakura = #596f88`；
- `@radish/ui` 的公开 `radishColors.brand` 对齐灰玉共享基线，`radishColors.primary` 保持墨蓝 action；
- 状态色、主题 ID、默认主题、权益、持久化、family-ui 固定副本与 Pencil 均未改变。

这不是全站机械改色。既有页面的品牌语义引用保持原位，T03 再通过代表路径识别具体误用；常规操作与链接不会因 `guofeng` 品牌改色而整体变绿。

## 2. 契约与实现边界

| 层级 | 本批结果 |
| --- | --- |
| family-ui L0 / L1 固定副本 | 保持 `v26.7.3` 原样，不改写上游文件 |
| Client 四主题 L1 | `guofeng` 使用确认的灰玉品牌；其余主题身份与状态值不变 |
| Radish L2 | brand hover 追溯显式 L1；link / primary 追溯 action |
| Ant Design 宿主 | 四主题分别提供 action primary / hover / link，不再把品牌色当常规主操作色 |
| 共享公开 API | `radishColors.brand` 与灰玉共享基线一致，README 示例同步 |
| 壳层边缘消费 | 主题预览、WebOS 弱装饰与 Dock 激活柔底使用品牌语义，不保留旧胭脂直值 |

## 3. 静态门禁

- `node --test --test-isolation=none Frontend/radish.client/tests/theme.test.ts Frontend/radish.ui/tests/familyThemeContract.test.ts`：`8 / 8`；
- `npm run test --workspace=@radish/ui`：`32 / 32`；
- `npm run test --workspace=radish.client`：`557 / 557`；
- `npm run test --workspace=radish.console`：`137 / 137`；
- `@radish/ui`、Client、Console 的 type-check 与 Lint 均通过；Client、Console production build 均通过，仅保留既有 chunk-size 提示；
- `npm run validate:baseline:quick`：通过，包含 `@radish/http 48 / 48`、上述三组前端测试、类型检查及仓库级守卫。

本批没有启动服务或浏览器。运行态四主题、PC / mobile、双语、keyboard focus 与 reduced-motion 证据统一留到 T03，避免在连续代码批中重复启动宿主和拆散验收矩阵。

## 4. 下一顺位

进入 `F4-R-T02`：在 Client `theme-tokens.css` 与 Console `index.css` 建立宿主级 `prefers-reduced-motion: reduce` 退出门禁，覆盖历史硬编码 transition / animation，同时保留文本、图标、进度与加载轮廓等状态双通道。T02 不逐文件复制规则、不引入用户设置或数据库字段，也不修改 Pencil。
