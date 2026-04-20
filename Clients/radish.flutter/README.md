# Radish Flutter

`radish.flutter` 是 `Phase 2-3 Flutter 客户端 MVP` 的仓库落点。

当前批次只提交最小客户端骨架，不直接铺完整业务页，也不复刻 WebOS 桌面工作台。

## 当前范围

- Android 起步的原生客户端壳层
- `discover / forum / docs / profile` 四个高价值入口占位
- 环境配置、认证存储占位与主题基线
- 与现有 Web / API 契约一致的复用边界
- Android 模拟器经 Gateway `https://localhost:5000` 的最小联调入口

## 当前不含

- Windows / Linux 平台目录的完整生成文件
- 聊天、完整通知中心、完整商城工作台、创作器
- “移动版 WebOS”

## 目录概览

```text
Clients/radish.flutter/
├── android/
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
4. Android 真机构建与最小联调复核

## 平台目录生成说明

Android 平台目录已经生成。后续如需补齐 Windows / Linux 平台工程，可在本目录执行：

```bash
flutter create --platforms=windows,linux .
```

## Android 模拟器联调

Flutter Android 开发态统一通过 Gateway 访问后端，入口保持为：

```text
https://localhost:5000
```

在 Android 模拟器中，`localhost` 默认指向模拟器自身。为保持 Gateway 本地开发证书的 `localhost` 主机名不漂移，启动应用前需要先建立 ADB 反向端口映射：

```powershell
adb reverse tcp:5000 tcp:5000
```

如果当前终端找不到 `adb`，使用 Android SDK 的完整路径执行，例如：

```powershell
D:\MyKits\android\platform-tools\adb.exe reverse tcp:5000 tcp:5000
```

不要把 Flutter 默认开发入口改成 `https://10.0.2.2:5000`。该地址虽然能指向宿主机，但会让请求主机名从 `localhost` 变成 `10.0.2.2`，与本地 Gateway 开发 HTTPS 证书不一致，容易在 TLS 握手阶段出现 `HandshakeException`。
