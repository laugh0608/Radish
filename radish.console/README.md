# Radish Console

Radish 管理控制台前端，基于 React 19 + TypeScript + Vite 构建。

## 功能

- 服务状态监控 - 显示核心服务的健康状态和响应时间
- 管理后台入口 - 未来将扩展为完整的管理后台功能
- 通过 Gateway 统一访问 - 所有请求通过 `/console` 路径代理

## 开发

### 启动开发服务器

```bash
# 从项目根目录
npm run dev --workspace=radish.console

# 或使用统一启动脚本
pwsh ./start.ps1   # Windows
./start.sh         # Linux/macOS
```

开发服务器运行在 `http://localhost:3002/`

### 通过 Gateway 访问

当 Gateway 和 console 开发服务器都在运行时，可以通过以下地址访问：

- `https://localhost:5000/console`

Gateway 会将 `/console/**` 请求转发到 `http://localhost:3002`。

## 使用 @radish/ui 组件库

Console 项目使用 `@radish/ui` 组件库：

```tsx
import { Button, Input, Modal } from '@radish/ui';
import { useToggle, useDebounce } from '@radish/ui/hooks';
import { formatDate, isEmail } from '@radish/ui/utils';
```

完整示例请查看 `src/examples/UIComponentsExample.tsx`。

## 项目结构

```
radish.console/
├── src/
│   ├── examples/          # 组件使用示例
│   ├── App.tsx            # 主应用组件
│   └── main.tsx           # 入口文件
├── public/                # 静态资源
├── package.json
└── README.md
```

## 注意事项

- 这是一个纯前端项目，没有后端代码
- API 请求和路由配置在 Gateway 端（YARP）
- 可以自由定制 UI，但需保持通过 `/console` 路径访问

## 文档

- [UI 组件库文档](../radish.docs/docs/UIComponentLibrary.md)
- [组件使用示例](./src/examples/UIComponentsExample.tsx)

---

**端口**: 3002
**访问路径**: `/console` (通过 Gateway)
**组件库**: @radish/ui
