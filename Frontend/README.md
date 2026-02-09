# Frontend

该目录集中管理 Radish 前端相关项目（npm workspaces）。

## 子项目

- `radish.client`：WebOS 桌面前端（用户侧）。
- `radish.console`：管理控制台前端（管理员侧）。
- `radish.ui`：共享 UI 组件库。
- `radish.http`：前端 HTTP 客户端与相关类型封装。

## 常用命令（在仓库根目录执行）

```bash
npm run dev --workspace=radish.client
npm run dev --workspace=radish.console
npm run type-check --workspace=@radish/ui
```

## 说明

- 采用 npm workspaces 管理依赖与联调。
- `radish.ui` 通过源码引用方式供 `radish.client` 与 `radish.console` 复用。
