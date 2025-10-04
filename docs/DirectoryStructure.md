# 萝卜目录结构 Radish Directory Structure

```bash
Radish/
├── angular/                      # Angular 后台管理前端项目
│   ├── e2e/                      # e2e 端到端测试目录
│   │   ├── src/                  # e2e 测试源码
│   │   ├── protractor.conf.js    # Protractor 测试框架配置
│   │   ├── tsconfig.json         # TypeScript 配置
│   │   └── ...                   # 其他配置文件
│   ├── src/                      # Angular 项目源码目录
│   │   ├── app/                  # Angular 应用组件和模块
│   │   ├── assets/               # 静态资源文件
│   │   ├── environments/         # 环境配置文件
│   │   ├── favicon.ico           # 网站图标
│   │   ├── index.html            # 应用主页面
│   │   ├── main.ts               # 应用入口文件
│   │   ├── polyfills.ts          # 浏览器兼容性填充
│   │   ├── styles.scss           # 全局样式文件
│   │   ├── test.ts               # 测试入口文件
│   │   └── ...                   # 其他源码文件
│   ├── .editorconfig             # 编辑器配置
│   ├── .eslintrc.json            # ESLint 代码规范配置
│   ├── .gitignore                # Git 忽略文件配置
│   ├── .prettierrc               # Prettier 代码格式化配置
│   ├── angular.json              # Angular 项目配置
│   ├── Dockerfile                # Docker 生产环境构建文件
│   ├── Dockerfile.local          # Docker 本地开发环境构建文件
│   ├── dynamic-env.json          # 动态环境变量配置
│   ├── karma.conf.js             # Karma 测试运行器配置
│   ├── nginx.conf                # Nginx 服务器配置
│   ├── package.json              # 项目依赖和脚本配置
│   ├── README.md                 # 项目说明文档
│   ├── start.ps1                 # PowerShell 启动脚本
│   ├── tsconfig.app.json         # 应用 TypeScript 配置
│   ├── tsconfig.json             # 根 TypeScript 配置
│   ├── tsconfig.spec.json        # 测试 TypeScript 配置
│   ├── web.config                # IIS Web 服务器配置
│   ├── yarn.lock                 # Yarn 依赖锁定文件
│   └── ...                       # 其他项目文件
├── docs/                         # 项目文档目录
│   └── ...                       # 各种项目文档
├── etc/                          # 项目配置目录
│   ├── abp-studio/run-profiles/  # ABP Studio 运行配置
│   │   ├── Default.abprun.json   # 默认运行配置文件
│   │   └── ...                   # 其他运行配置
│   └── ...                       # 其他配置目录
├── modules/                      # DDD 领域驱动设计附加模块
│   ├── contohtri/                # ComToHtri 模块项目
│   │   ├── src/                  # ComToHtri 模块源码目录
│   │   │   ├── ComToHtri.Application/                  # 应用服务层
│   │   │   ├── ComToHtri.Application.Contracts/        # 应用服务契约
│   │   │   ├── ComToHtri.Domain/                       # 领域层
│   │   │   ├── ComToHtri.Domain.Shared/                # 共享领域层
│   │   │   ├── ComToHtri.EntityFrameworkCore/          # EntityFramework 数据访问层
│   │   │   ├── ComToHtri.HttpApi/                      # HTTP API 接口
│   │   │   ├── ComToHtri.HttpApi.Client/               # HTTP API 客户端
│   │   │   ├── ComToHtri.Installer/                    # 模块安装器
│   │   │   ├── ComToHtri.MongoDB/                      # MongoDB 数据访问层
│   │   │   ├── ComToHtri.Web/                          # Web 项目
│   │   │   └── ...               # ComToHtri 其他模块相关目录
│   │   ├── test/                 # ComToHtri 模块测试目录
│   │   │   ├── ComToHtri.Application.Tests/            # 应用服务测试
│   │   │   ├── ComToHtri.Domain.Tests/                 # 领域层测试
│   │   │   ├── ComToHtri.EntityFrameworkCore.Tests/    # 数据访问层测试
│   │   │   ├── ComToHtri.MongoDB.Tests/                # MongoDB 测试
│   │   │   ├── ComToHtri.TestBase/                     # 测试基类库
│   │   │   └── ...               # 其他测试项目
│   │   ├── .editorconfig         # 编辑器配置
│   │   ├── .gitignore            # Git 忽略文件配置
│   │   ├── common.props          # 公共项目属性
│   │   ├── ComToHtr.abpmdl       # ABP 模块定义文件
│   │   ├── ComToHtri.sln         # Visual Studio 解决方案文件
│   │   └── ...                   # 其他模块文件
│   └── ...                       # 其他附加模块
├── react/                        # React 前端项目
│   ├── public/                   # 公共静态资源
│   │   ├── vite.svg              # Vite 图标
│   │   └── ...                   # 其他公共资源
│   ├── src/                      # React 源码目录
│   │   ├── assets/               # 项目资源文件
│   │   │   ├── react.svg         # React 图标
│   │   │   └── ...               # 其他资源文件
│   │   ├── App.css               # 应用样式文件
│   │   ├── App.jsx               # 应用根组件
│   │   ├── index.css             # 全局样式文件
│   │   ├── main.jsx              # 应用入口文件
│   │   └── ...                   # 其他组件和工具
│   ├── .gitignore                # Git 忽略文件配置
│   ├── eslint.config.js          # ESLint 配置
│   ├── index.html                # HTML 模板文件
│   ├── package-lock.json         # NPM 依赖锁定文件
│   ├── package.json              # 项目依赖和脚本配置
│   ├── react.iml                 # IntelliJ IDEA 模块文件
│   ├── README.md                 # 项目说明文档
│   ├── start.ps1                 # PowerShell 启动脚本
│   ├── vite.config.js            # Vite 构建工具配置
│   └── ...                       # 其他 React 项目文件
├── src/                          # 后端 API 核心项目
│   ├── Radish.Application                              # 应用服务层
│   ├── Radish.Application.Contracts                    # 应用服务契约
│   ├── Radish.DbMingrator                              # 数据库迁移项目
│   ├── Radish.Domain                                   # 领域层
│   ├── Radish.Domain.Shared                            # 共享领域层
│   ├── Radish.HttpApi                                  # HTTP API 接口定义
│   ├── Radish.HttpApi.Client                           # HTTP API 客户端
│   ├── Radish.HttpApi.Host                             # API 宿主项目
│   ├── Radish.MongoDB                                  # MongoDB 数据访问层
│   └── ...                       # 其他后端项目
├── test/                         # 后端单元测试项目
│   ├── Radish.Application.Tests                        # 应用服务测试
│   ├── Radish.Domain.Tests                             # 领域层测试
│   ├── Radish.HttpApi.Client.ConsoleTestApp            # API 客户端控制台测试应用
│   ├── Radish.MongoDB.Tests                            # MongoDB 数据访问测试
│   ├── Radish.TestBase                                 # 测试基类库
│   └── ...                       # 其他测试项目
├── .editorconfig                 # 编辑器配置
├── .gitattrbutes                 # Git 属性配置
├── .gitignore                    # Git 忽略文件配置
├── adb-command-prompt.png        # 安卓 ADB 命令截图
├── common.props                  # 公共项目属性配置
├── LICENSE.txt                   # 项目许可证
├── NuGet.Config                  # NuGet 包管理器配置
├── Radish.abpmdl                 # ABP 模块定义文件
├── Radish.abpsln                 # ABP Studio 解决方案文件
├── Radish.sln                    # Visual Studio 解决方案文件
├── Radish.sln.DotSettings        # ReSharper 配置
├── README.md                     # 项目主说明文档
└── ...                           # 其他根目录文件
```
