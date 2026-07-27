# F4-L-C Wiki 附件正式 Web 受保护资源

## 结论

F4-L-C 已完成正式 Web 实现与静态回归，下一顺位进入 F4-L-D 成组验收与专题关闭。

本批没有新增页面族或修改 Pencil，也没有把认证状态下沉到 `@radish/ui`。Author、Authenticated / Restricted Docs、Revision 与 Console 审核统一通过宿主注入的认证 Blob 加载契约消费私有附件；Public + Published 正文仍使用稳定公开 URL。

## 已完成范围

- `@radish/http` 新增统一附件 Blob 读取入口，复用现有 `apiFetch`、认证刷新和 API 配置，不另建 fetch / axios 封装；同时修正 `apiFetch` 覆盖调用方 `AbortSignal` 的根因，使附件切换和卸载能够真实取消请求。
- `@radish/ui` 的 Markdown 渲染器仅接收宿主注入的受保护附件契约，不感知账号、Token 或 Wiki API；图片缩略图、灯箱原图和普通文件均支持加载、失败与重试状态。
- object URL 注册表按账号、文档、Draft / Revision、正文版本和权限能力建立作用域；内容替换、账号 / 文档 / Revision 切换、失败、重试和卸载会取消旧请求并释放同批 URL，旧代次结果不能回写新页面。
- `/docs` 对非 Public 文档使用可选认证的一般读取契约，修正此前 `PublicGet* + withAuth=false` 使 Authenticated / Restricted 文档无法进入页面的边界；受保护页面不生成公开 SEO head 或公开快照。
- `/docs/mine`、创建 / 编辑预览、Revision 回看和封面预览已接入同一受保护资源契约；上传后未保存附件继续以认证资源预览，保存失败不伪装为已绑定。
- Console `/documents` 的正式详情、当前正文与待审草稿对照、Revision 和封面均接入受保护资源加载，不增加新的治理动作或导航。
- 中英文加载、失败、重试与灯箱无障碍标签已补齐；图片与文件支持键盘焦点，灯箱支持 Enter / Space 打开，既有 PC / mobile 响应式结构保持不变。
- 失败状态和用户文案不暴露内部资源路径、鉴权细节或服务端错误正文。

## 验证结果

- 四个前端 workspace 的 type-check 与 lint：全部通过。
- `@radish/http`：22 项测试通过。
- `@radish/ui`：27 项测试通过。
- `radish.client`：461 项测试通过，production build 通过。
- `radish.console`：61 项测试通过，production build 通过。
- Wiki Attachment / Access / Controller 后端定向测试：37 项通过，2 项 PostgreSQL 环境用例按配置跳过。
- `npm run validate:baseline:quick`：通过。
- `npm run check:docs`、`npm run check:repo-hygiene:changed` 与 `git diff --check`：通过。

## 下一批边界

F4-L-D 使用 Owner、Accepted Collaborator、Revoked Collaborator、Restricted Reader、Reviewer 和匿名用户完成 Gateway 运行态矩阵，覆盖完整文档状态、图片 / 文件 / 封面 / token、失权与恢复、`zh / en × PC / mobile`、多标签、Back / Forward、临时数据清理、六库完整性和严格 migration verify。

本批未获得并且不需要服务启动授权，因此没有启动 API、Auth、Gateway、Client 或 Console，也没有把静态验证写成真实运行态结论；F4-L 在 F4-L-D 完成前不关闭。
