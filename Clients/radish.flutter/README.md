# Radish Flutter

`radish.flutter` 是 `Phase 2-3 Flutter 客户端 MVP` 的仓库落点。

当前批次只提交最小客户端骨架，不直接铺完整业务页，也不复刻 WebOS 桌面工作台。

## 当前范围

- Android 起步的原生客户端壳层
- `discover / forum / docs / profile` 四个高价值入口占位
- 环境配置、认证存储占位与主题基线
- 与现有 Web / API 契约一致的复用边界

## 当前不含

- Android / Windows / Linux 平台目录的完整生成文件
- 聊天、完整通知中心、完整商城工作台、创作器
- “移动版 WebOS”

## 目录概览

```text
Clients/radish.flutter/
├── lib/
│   ├── app/
│   ├── core/
│   ├── features/
│   └── shared/
├── test/
├── analysis_options.yaml
├── pubspec.yaml
└── .gitignore
```

## 后续接线顺序

1. 登录 / 会话恢复
2. `discover` 首批真实页面
3. `forum / docs / profile` API 接线
4. Android 真机构建与最小联调

## 平台目录生成说明

当 Flutter CLI 在本机环境可稳定使用后，可在本目录补齐平台工程：

```bash
flutter create --platforms=android,windows,linux .
```
