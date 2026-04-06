# M14 部署后最小复核记录模板

> 本页用于把 `M14` 的测试部署 / 生产部署复核结果整理成统一记录。
>
> 关联入口：
>
> - [M14 宿主运行首轮执行清单](/guide/m14-host-runtime-checklist)
> - [M14 宿主运行与最小可观测性基线（重定义）](/guide/m14-host-runtime-observability-baseline)
> - [部署与容器指南](/deployment/guide)
> - [变更回归记录模板](/guide/change-regression-record-template)

## 适用场景

以下场景建议使用本模板补一份单独记录：

- 测试部署完成后，需要确认“容器能启动”已经进一步闭合为“入口可访问、登录回调可用”
- 生产部署或生产口径演练完成后，需要留一份固定的外部访问复核记录
- 宿主、反代、证书、`RADISH_PUBLIC_URL`、OIDC 回调链或部署编排刚发生变化
- 需要把 `validate:baseline:host -> check:host-runtime -> collect:m14-host-record` 的结果与部署侧人工复核串成同一份材料

## 记录原则

- 启动前与启动后自动化结果优先直接引用现成报告，不要手工改写成新说法
- 测试部署与生产部署都沿用同一套字段，只在“额外必填项”上区分
- 事实、动作、结论分开写，不把“检查项”和“解释”混在同一条里
- 若当前环境尚不具备真实域名、反代或正式证书，可明确写“未执行 / 不适用”，不要伪装成通过

## 默认引用顺序

推荐先完成以下记录来源，再填写本模板：

```bash
npm run validate:baseline:host -- --report-file .tmp/baseline-host-report.md
npm run check:host-runtime -- --report-file .tmp/host-runtime-report.md
npm run collect:m14-host-record
```

默认情况下，上述命令会生成：

- `.tmp/baseline-host-report.md`
- `.tmp/host-runtime-report.md`
- `.tmp/m14-host-maintenance-record.md`

本模板应优先直接引用这三份结果，而不是重新手工概括“应该没问题”。

## 最小字段

建议每份部署后复核记录至少包含：

1. 记录信息
   - 日期、记录人、环境类型、范围
2. 部署口径
   - `base + test` 或 `base + prod`
   - 外部访问地址
   - 镜像版本 / tag
3. 自动化前置
   - `validate:baseline:host`
   - `check:host-runtime`
   - `collect:m14-host-record`
4. 部署配置复核
   - `RADISH_PUBLIC_URL`
   - 证书来源 / 挂载目录
   - 反代头或容器内 HTTPS 口径
5. 外部访问复核
   - `/health`
   - `/`
   - `/console/`
   - `/scalar`
   - 登录 / 回调 / 登出
6. 额外链路复核
   - `userinfo`
   - 受保护接口
   - 反代头 / Issuer / 回调地址一致性
7. 故障归类 / 环境边界
   - 无 / 配置前提 / 运行态 / 反代链路 / 外部环境边界
8. 结论与后置项

## 推荐模板

```md
## M14 部署后最小复核记录（<test|prod>）

- 记录日期：YYYY-MM-DD
- 记录人：<name>
- 环境类型：测试部署 / 生产部署
- 记录范围：<topic>

### 部署口径

- Compose 组合：`base + test` / `base + prod`
- 外部访问地址：`https://...`
- 镜像版本：`<tag>`
- 证书来源：<path / volume / secret / 自动生成>

### 自动化前置

- `npm run validate:baseline:host`：通过 / 阻塞 / 未执行
- `npm run check:host-runtime`：通过 / 阻塞 / 未执行
- `npm run collect:m14-host-record`：已生成 / 未生成
- 维护记录路径：`.tmp/m14-host-maintenance-record.md` / <custom path>

### 配置一致性复核

- `RADISH_PUBLIC_URL`：一致 / 不一致 / 未执行
- `OpenIddict__Server__Issuer`：一致 / 不一致 / 未执行
- 证书路径 / 挂载：正确 / 异常 / 未执行
- 公开入口口径：
  - 测试部署：Gateway 容器内 HTTPS / 自签名证书
  - 生产部署：外层反代 HTTPS / 容器内 HTTP
- 反代头：
  - `Host`：正常 / 异常 / 不适用
  - `X-Forwarded-Proto`：正常 / 异常 / 不适用
  - `X-Forwarded-Host`：正常 / 异常 / 不适用

### 外部访问复核

- `/health`：通过 / 阻塞 / 未执行
- `/`：通过 / 阻塞 / 未执行
- `/console/`：通过 / 阻塞 / 未执行
- `/scalar`：通过 / 阻塞 / 未执行
- `radish-client` 登录 / 回调 / 登出：通过 / 阻塞 / 未执行
- `radish-console` 登录 / 回调 / 登出：通过 / 阻塞 / 未执行
- `radish-scalar` 登录 / 回调 / 登出：通过 / 阻塞 / 未执行

### 额外链路复核

- `userinfo`：通过 / 阻塞 / 未执行
- 受保护接口：通过 / 阻塞 / 未执行
- 说明：
  - <only write facts actually checked>

### 故障归类 / 环境边界

- 归类：无 / 配置前提 / 运行态 / 反代链路 / 外部环境边界
- 说明：<none or summary>

### 结论

- <current decision>

### 风险 / 后置项

- <none or remaining item>
```

## 测试部署额外关注项

- 浏览器对自签名证书弹告警属于预期行为，不应直接记为宿主异常
- 如果使用 `https://IP:port`，应确认 `RADISH_PUBLIC_URL`、Gateway 证书 host 与实际访问地址一致
- `AuthUi__ShowTestAccountHint=true` 时可保留测试账号提示，但不要把这个测试口径误记到生产记录里

## 生产部署额外关注项

- `RADISH_PUBLIC_URL` 必须与真实外部 HTTPS 域名完全一致
- 外层 Nginx / Traefik / Caddy 需要保留 `Host`、`X-Forwarded-Proto`、`X-Forwarded-Host`
- 不要用 `localhost` 或容器内地址代替外部真实入口做 OIDC 回调验证
- 若本轮只完成容器启动和 `/health`，但未完成外部域名登录 / 回调 / `userinfo` / 受保护接口验证，应明确记为“部分完成”

## 推荐放置位置

优先顺序建议如下：

1. 部署记录或运维记录
2. PR / 发布说明
3. 阶段汇总或周志

如果本轮同时涉及业务改动和部署改动，可把本模板作为“部署复核”小节附加到 [变更回归记录模板](/guide/change-regression-record-template) 之下，而不是重新写第二套结论。

## 当前样例

当前仓库已补一份真实 `M14` 样例记录：

- [M14 宿主维护记录样例（2026-04-06）](/guide/m14-host-maintenance-record-example)
- [M14 部署后最小复核记录（2026-04-06）](/guide/m14-deployment-review-record-2026-04-06)

- 前者覆盖“启动前 + 启动后默认主路径真实通过”
- 后者覆盖“测试部署 + 生产部署首轮真实复核通过”
