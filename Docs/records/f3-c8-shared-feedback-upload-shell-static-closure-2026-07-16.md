# F3-C8 共享反馈、上传与公开壳层静态收口记录

> 日期：2026-07-16（Asia/Shanghai）
>
> 范围：代码检查点 `d5341095` 与最终收口提交 `762e32ac`，覆盖共享反馈、附件安全、分片上传、原子限流、裁切生命周期、公开 head、论坛事务和动态错误参数加固。

## 结论

- `F3-C8` 的 client 低频页面、公开承诺、共享反馈 / 上传组件和公开页面 head 已完成本地实现与静态收口，下一顺位进入 `F3-C9` Console 剩余管理域。
- 本批只把系统词元、状态、动作、错误和格式化参数纳入宿主双语资源；运营承诺正文、用户内容与文件名继续按来源原文展示。
- 本批已收口纳入范围的上传输入、存储路径、处理生命周期、业务绑定与写事务问题，没有新增第二套 HTTP、上传或宿主 i18n 状态；附件业务域可见性和持久化配额结算作为明确维护项保留，不据此宣称私有附件治理已经完成。
- 本批未启动 API、Auth、Gateway、client 或 Console，未执行 PC / mobile 浏览器 smoke；因此本记录只证明代码与静态契约收口，不代表运行态专题验收。

## 实现边界

### 共享反馈与上传

- `@radish/ui` 由宿主传入 Markdown、裁切、弹层与上传 labels，真实消费者统一处理进度、忙碌态、错误与关闭约束。
- 非幂等上传只提交一次，不再对网络、4xx、5xx 或非 JSON 失败自动重试；调用者可通过 `AbortSignal` 主动取消。
- 图片类型由共享 accept / 校验入口声明并明确排除 SVG，client 与 Console 不再各自维护硬编码图片白名单。
- `ImageCropper`、`Modal` 与头像上传共享同一处理生命周期：裁切或上传期间锁定关闭，图片切换、取消和卸载后丢弃失效任务结果。
- 头像绑定与清空复用 `@radish/http`，保留统一认证、取消信号和服务端结构化错误，不再由组件直接 `fetch` 并吞掉错误契约。
- 分片自动开始对每个文件只触发一次；分片阶段可真实中止 XHR，进入服务端最终合并后关闭取消入口，不把无法撤销的附件落库伪装为已取消。

### 服务端附件信任边界

- `businessType` 只接受稳定白名单，并统一用于普通上传与分片会话创建；Console 专属类型同时校验对应资源权限，业务关联只由所属业务服务完成。
- 存储路径使用规范化后的 containment 校验，拒绝绝对路径、父目录逃逸和存储根路径本身。
- 服务端根据扩展名与文件签名判定 MIME，不信任 multipart `Content-Type`；内容与声明不一致返回稳定错误。
- 图片处理失败会清理原文件、缩略图、多尺寸输出和临时文件；附件持久化成功后的记账失败只记录诊断，不把已成功的非幂等上传伪装为失败。
- 用户上传根目录不再经 `/uploads/**` 静态直出；受控读取统一阻断 disabled / deleted，私有附件只允许上传者或 `System / Admin`，token 下载通过独立入口且不能绕过文件状态，只保留 `/uploads/DefaultIco/**` 可信内置图标。
- 孤立附件清理会识别启用中的 favicon 受控资源 URL、Wiki 当前正文和全部可回滚 revision 正文；替换、恢复默认或删除正文引用后，旧附件才重新进入清理候选。
- 分片链路补齐属主 / 租户、精确分片长度、会话串行、跨租户清理与 15 分钟终态配额重放；当前仍以单实例、本地临时目录和缓存预留为停止线。

### 公开壳层与写入一致性

- `/legal` 进入 Workbench 正式入口；浏览器 React 根只挂载一个 `ToastContainer`。
- 公开页面只提交 head 快照，唯一 lifecycle owner 负责 canonical、Open Graph、Twitter 与 JSON-LD DOM 写入；路由切换或离开公开壳层时清理旧快照。
- 论坛发布 ledger、业务实体与 Reliable Outbox 处于同一 `[UseTran]` 事务。`ContentSubmission` 唯一冲突通过数据库保存点恢复，避免 PostgreSQL 事务进入 aborted 状态。
- 成功记账缺失或更新未命中会 fail closed；事务提交失败保留原始异常，回滚失败时聚合两类诊断。
- `MessageArguments` 只允许短安全标量数组，服务端统一规范化后交给前端翻译器；业务控制流仍只依赖 HTTP status、`Code` 和稳定状态。

## 已知维护边界

- 附件已落库但会话完成状态持续回写失败且响应丢失时，仍需 attachment session correlation / 唯一约束找回既有附件。
- 普通上传缺少 durable quota settlement；分片终态重放也只覆盖缓存预留仍存在且未发生批次饥饿的窗口，最终需持久化 ledger / outbox。
- 分片临时存储与会话锁只支持严格单实例；前端不承诺跨请求暂停 / 恢复。
- `Chat / Document / Wiki` 的业务域 ACL 与历史数据迁移尚未完成，这些类型当前不得承载敏感文件，不能把统一 `IsPublic = true` 语义误写成私有附件治理已经完成。
- 历史正文、Wiki revision 与 favicon 配置中的 `/uploads/**` / 旧域名直链尚需生产数据盘点和迁移；当前源码与配置断言也不能替代真实 Gateway 路由验收。

## 静态验证

- `radish.client`：415 项测试、type-check、lint、production build 通过。
- `radish.console`：52 项测试、strict type-check、lint、production build 通过。
- `@radish/ui`：21 项测试、type-check、lint 通过。
- `@radish/http`：13 项测试、type-check、lint 通过。
- `Radish.Api.Tests`：814 项通过、12 项 PostgreSQL 环境用例按配置跳过、0 项失败。
- `dotnet build Radish.slnx -c Debug --no-restore`：0 warning、0 error。
- `npm run validate:baseline:quick` 与 `git diff --check` 通过；changed repo hygiene 未发现文本卫生错误，保留文档建议篇幅提醒。
- client production build 保留既有大 chunk 提示；Console production build 无警告。

## 下一顺位

1. `F3-C9` 先盘点 Console 角色权限、分类标签、表情、经验和萝卜管理的正式路由、实际消费者与既有专题边界。
2. 复用当前 `@radish/http`、共享上传类型、结构化错误和 locale formatter，不建立 Console 专属平行契约。
3. `F3-C9` 静态收口后再进入 `F3-D` 代表矩阵验收；服务启动与浏览器 smoke 必须重新取得当轮授权。
