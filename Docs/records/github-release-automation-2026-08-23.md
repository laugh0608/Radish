# GitHub Release 自动化实现记录（2026-08-23）

## 结论

`Docker Images` workflow 已在既有 Candidate Quality、五镜像漏洞策略、构建与推送之后增加 GitHub Release 收口任务。发布轨道固定为：

| tag 轨道 | GitHub Release 行为 |
| --- | --- |
| `v*-dev` | 不创建 Release 页面 |
| `v*-test` | 创建 Pre-release，并显式保持 `latest=false` |
| `v*-release` | 创建正式 Release，并显式标记为 Latest |

本批只建立未来 tag 的自动化，不补建或改写历史 Release，也不创建新 tag。

## 实现边界

- Release job 显式依赖 `candidate-quality`、`prepare`、`backend-images`、`frontend-image`；任一前置任务失败时不会创建 Release。
- workflow 顶层继续保持 `contents: read / packages: write`；只有 Release job 获得 `contents: write`。
- 创建时使用 `--verify-tag`，只接受已存在的远端 tag，不在 Release job 内创建或移动 tag。
- 自动 notes 以上一个在当前提交历史中可达的正式 `v*-release` tag 为起点；测试 Pre-release 不会成为下一次正式 notes 的基线。
- Release 已存在时只核对 `draft / prerelease` 状态；状态正确则幂等成功，轨道不符则失败，不自动覆盖。
- Release 页面只保留 GitHub 自动源码归档、生成 notes 与五个 GHCR 镜像坐标；不自动上传 Flutter APK、桌面安装包或临时 Trivy artifact。
- GitHub Release 创建不代表测试部署或生产部署完成，部署仍是独立授权动作。

## 验证证据

- `npm run check:repo-quality-contract`：通过，新增 job、依赖、权限、轨道、tag 校验与 notes 基线均受静态契约约束。
- `npm run validate:baseline:quick`：通过，包含版本契约、镜像漏洞策略、四个前端 workspace 类型与测试、权限、敏感字面量、时间语义、LongId 和 Repo Quality 契约等基线。
- `npm run check:docs`、`npm run check:repo-hygiene:changed`、`npm run lint:changed`：通过，无无效文档链接、文本卫生或适用前端 Lint 问题。
- workflow YAML 解析：通过。
- Release step 提取后执行 `bash -n`：通过。
- 使用从 workflow 提取的真实 shell 逻辑和 mock `gh` 验证：
  - 新 `v26.9.1-test`：生成 Pre-release 参数、`latest=false`，notes 基线为 `v26.8.1-release`。
  - 新 `v26.9.1-release`：生成正式 Latest 参数，notes 基线为 `v26.8.1-release`。
  - 已存在且同轨道：幂等成功，不再次创建。
  - 已存在但轨道不符：退出码 `1`，不会覆盖。

## 首次线上证据边界

本批未推送 tag，也未触发真实 GitHub Actions 或创建真实 GitHub Release。下一枚新的 `v*-test` 或 `v*-release` tag 完成 workflow 后，应在发布记录中补充实际 Release URL 与 GitHub Actions 结论。
