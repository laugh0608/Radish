# Development Framework

> 以下内容参考自 Gemini-2.5-Flash。
>
> 开发框架图请点：[Radish](https://affine.imbhj.net/workspace/e54b7d62-20a6-473e-bfc8-0e0f1592b17c/i2ZYOGr95mO5T88PhTKr8) 。

## ABP & React/Angular & MongoDB & Docker 论坛项目开发大纲

### 项目名称

Radish，萝卜。

### 项目概述

本项目旨在构建一个高性能、可扩展的现代化论坛平台。后端基于ABP框架的分层架构和领域驱动设计（DDD），使用MongoDB作为主要数据存储，并提供完善的RESTful API。后台管理界面采用ABP自带的Angular UI，提供全面的用户、权限和内容管理功能。前端用户界面则使用React开发，专注于提供流畅、响应式的用户体验，实现核心论坛交互。整个项目将通过Docker进行容器化，实现一键启动和部署。未来规划包括积分系统和商城系统，以增强用户互动和平台粘性。

### 1. 技术栈概览

* **后端框架:** [ABP Framework](https://abp.io) (建议 v7.x 或 v8.x，基于 .NET 8)
  * **架构模式:** DDD (领域驱动设计) 分层架构
  * **核心模块:** `Volo.Abp.Identity`, `Volo.Abp.PermissionManagement`, `Volo.Abp.AuditLogging`, `Volo.Abp.SettingManagement` 等。
* **API 管理/调试:** [Swagger UI](https://swagger.io/) / [Scalar](https://scalar.com/) (ABP框架自带集成)
* **后台管理UI:** [ABP Angular UI](https://docs.abp.io/en/abp/latest/UI/Angular)
* **前端用户UI:** [React](https://react.dev/) (配合 [Vite](https://vitejs.dev/) 或 [Next.js](https://nextjs.org/) 等构建工具，建议Vite简化配置)
* **数据库:** [MongoDB](https://www.mongodb.com/) (建议 v6.x 或更高版本)
* **容器化:** [Docker](https://www.docker.com/) & [Docker Compose](https://docs.docker.com/compose/)
* **身份认证/授权:** JWT (JSON Web Tokens) + ABP IdentityServer4 (或 ABP内置的OpenIddict)
* **辅助工具:** Git, VS Code/Visual Studio

### 2. 系统架构

#### 2.1 高层架构图

```mermaid
graph TD
    subgraph Frontend Layer
        A[React UI Forum Portal] -->|API Calls| C(ABP .Host - API Gateway)
        B[ABP Angular UI Admin Portal] -->|API Calls| C
    end

    subgraph Backend Layer ABP Framework
        C -->|HTTP/gRPC| D(ABP .Application)
        D -->|Uses| E(ABP .Domain)
        E -->|Uses/Persists| F(ABP .Domain.MongoDB - Infrastructure)
        F -->|MongoDB Driver| G[(MongoDB Database)]
    end

    subgraph Deployment
        H[Docker Compose] --> C
        H --> F
        H --> G
        H --> A
        H --> B
    end

    C -->|Swagger/Scalar| I[API Documentation]
    C -->|Authentication/Authorization| J[IdentityServer/OpenIddict]
```

#### 2.2 ABP 后端分层详细说明 (DDD)

* **ForumProject.Host:**
  * ASP.NET Core Web Application，作为API网关。
  * 配置身份认证 (JWT/OpenIddict)。
    * 集成Swagger/Scalar，提供API文档和调试界面。
    * 配置CORS (跨域资源共享)。
    * 加载并暴露所有应用服务 (Application Services)。
  * **Docker:** 将此项目容器化为API服务。
* **ForumProject.Application.Contracts:**
  * 定义应用层与表示层 (UI) 之间的数据传输对象 (DTOs)。
  * 定义应用服务接口 (Application Service Interfaces)。
  * **DDD:** DTOs作为输入/输出，是聚合根或实体的扁平化表示。
* **ForumProject.Application:**
  * 实现应用服务接口。
  * 负责业务逻辑的编排，调用领域服务和仓储。
  * 处理事务、权限检查、数据转换。
  * **DDD:** 作为领域层和表示层之间的协调者，不包含核心业务规则，只负责流程控制。
* **ForumProject.Domain.Shared:**
  * 包含所有层共享的常量、枚举、全局异常定义等。
* **ForumProject.Domain:** (DDD 核心层)
  * **聚合根 (Aggregate Roots):** `User` (来自Identity), `Category`, `Post`, `ShopItem`, `UserPoints`。负责保证自身内部及关联实体的业务不变性。
  * **实体 (Entities):** `Comment`, `Tag`, `PointTransaction`, `UserInventory` (可能依附于聚合根)。拥有唯一标识符和生命周期。
  * **值对象 (Value Objects):** `PostTitle`, `Content` (如果需要封装特定行为), `Money` (用于积分/价格)。没有唯一标识符，不可变。
  * **领域服务 (Domain Services):** 处理跨多个聚合根的业务逻辑，例如 `PostManager` 可能包含置顶、精华等逻辑，`PointCalculator` 计算积分。
  * **仓储接口 (Repository Interfaces):** 定义领域层访问持久化数据的契约，例如 `IPostRepository`, `ICategoryRepository`。
  * **领域事件 (Domain Events):** 用于解耦业务逻辑，例如 `PostCreatedEvent`, `PostLikedEvent`, `CommentAcceptedEvent` 等，积分系统将订阅这些事件。
* **ForumProject.Domain.MongoDB:** (基础设施层)
  * 实现领域层定义的仓储接口，将领域对象持久化到MongoDB数据库。
  * 配置MongoDB连接字符串。
  * 利用ABP提供的MongoDB集成，如 `IMongoDbContext`, `IMongoDbRepository<TEntity>`。
  * **Docker:** 无需独立容器，作为Host项目的依赖库。
* **ForumProject.DbMigrator:**
  * 独立的控制台应用，用于执行数据库初始化、数据播种 (Seed Data)、MongoDB索引创建等。
  * **Docker:** 通常作为一次性运行的容器服务，在其他服务启动前执行。

### 3. 核心功能模块 (MVP - 最简可行产品)

#### 3.1 用户与身份管理 (基于 `Volo.Abp.Identity` 模块)

* **后端 (ABP):**
  * 使用 ABP Identity 模块提供的 `User` 聚合根和 `UserManager` 领域服务。
  * 提供用户注册 (邮箱/用户名), 登录 (JWT认证), 登出API。
  * 提供用户个人资料查询、修改API (昵称、头像URL、简介等)。
  * 密码重置、修改API。
  * **Admin UI (Angular):**
    * ABP Identity 模块自带的用户管理界面：用户列表、用户详情、创建/编辑/删除用户、激活/禁用用户、设置密码等。
  * **Frontend UI (React):**
    * 用户注册页面。
    * 用户登录页面。
    * 个人中心页面：显示用户头像、昵称、简介、积分等。
    * 编辑个人资料页面。
    * 修改密码页面。
    * 头像上传功能 (后端集成文件存储服务)。

#### 3.2 权限与角色管理 (基于 `Volo.Abp.PermissionManagement` 模块)

* **后端 (ABP):**
  * 使用 ABP Permission Management 模块提供的 `Role` 聚合根和 `PermissionGrant` 实体。
  * 定义细粒度权限：例如 `Post.Create`, `Post.Edit`, `Post.Delete`, `Comment.Create`, `Comment.Moderate`, `Category.Manage` 等。
  * 应用服务在执行操作前进行权限检查 (`_permissionChecker.CheckAsync(...)` 或 `[Authorize]` 属性)。
  * **Admin UI (Angular):**
    * ABP Permission Management 模块自带的角色管理界面：创建/编辑/删除角色。
    * 权限配置界面：为角色分配/撤销具体权限。
    * 用户-角色关联管理。
  * **Frontend UI (React):**
    * 根据当前用户的权限，动态显示或隐藏某些UI元素或功能按钮 (例如，只有版主才能看到审核按钮)。
    * 前端权限判断通常依赖于后端返回的用户权限列表。

#### 3.3 论坛核心功能

##### 3.3.1 版块/分类管理

* **后端 (ABP - DDD):**
  * **聚合根:** `Category` (Id, Name, Slug, Description, Order, IconUrl, IsActive)。
  * **仓储接口:** `ICategoryRepository` (CRUD)。
  * **应用服务:** `CategoryAppService` (Create, Update, Delete, Get, GetList)。
  * **Admin UI (Angular):**
    * 版块列表、创建/编辑/删除版块、调整排序、设置图标等。
  * **Frontend UI (React):**
    * 首页展示所有活跃的版块列表。
    * 点击版块进入该版块的帖子列表页。

##### 3.3.2 帖子发布与管理

* **后端 (ABP - DDD):**
  * **聚合根:** `Post` (Id, Title, Slug, Content, AuthorId, CategoryId, Tags, CreationTime, LastModificationTime, Status-Draft/Published/Sticky/Elite, ViewCount, LikeCount, CommentCount)。
  * **值对象:** `PostTitle`, `PostContent` (如果需要封装特定验证或行为)。
  * **仓储接口:** `IPostRepository` (CRUD, 分页查询, 条件过滤)。
  * **领域服务:** `PostManager` (处理帖子状态变更-置顶/精华/审核, 增加浏览量等复杂业务逻辑)。
  * **领域事件:** `PostCreatedEvent`, `PostEditedEvent`, `PostLikedEvent`, `PostViewedEvent`。
  * **应用服务:** `PostAppService` (Create, Update, Delete, Get, GetList - 支持按分类、标签、状态、排序等查询)。
  * **Admin UI (Angular):**
    * 帖子列表、搜索、筛选 (按分类、作者、状态)。
    * 帖子详情、编辑、删除。
    * 审核帖子 (设置为发布/草稿)。
    * 管理帖子状态 (置顶、精华)。
  * **Frontend UI (React):**
    * **发布帖子页面:** 富文本编辑器 (例如 Quill 或 TinyMCE)，选择分类，输入标签。
    * **帖子列表页:** 展示帖子列表 (分页、按最新/热门/精华/置顶排序)，搜索功能，按分类筛选。
    * **帖子详情页:** 显示帖子标题、内容、作者信息、发布时间、点赞数、浏览量。
    * **点赞/取消点赞:** 用户可以对帖子进行点赞 (记录用户ID和帖子ID)。
    * **编辑/删除帖子:** 帖子作者或拥有相应权限的用户可以操作。

##### 3.3.3 用户回帖/评论管理

* **后端 (ABP - DDD):**
  * **实体:** `Comment` (Id, Content, AuthorId, PostId, ParentCommentId-可选，用于回复评论, CreationTime, LastModificationTime, LikeCount, Status)。
  * **仓储接口:** `ICommentRepository` (CRUD, 查询某个帖子的评论列表)。
  * **领域服务:** `CommentManager` (处理评论审核等)。
  * **领域事件:** `CommentCreatedEvent`, `CommentLikedEvent`。
  * **应用服务:** `CommentAppService` (Create, Update, Delete, GetListByPostId)。
  * **Admin UI (Angular):**
    * 评论列表、搜索、筛选。
    * 评论详情、编辑、删除。
    * 审核评论。
  * **Frontend UI (React):**
    * **评论区:** 在帖子详情页下方显示评论列表。
    * **发表评论:** 用户可以在评论区发表新评论。
    * **回复评论:** 支持对其他评论进行回复 (可考虑扁平化或多级嵌套)。
    * **评论点赞/取消点赞:** 用户可以对评论进行点赞。
    * **编辑/删除评论:** 评论作者或拥有相应权限的用户可以操作。

### 4. 附加功能模块 (未来迭代)

#### 4.1 积分系统

* **后端 (ABP - DDD):**
  * **聚合根:** `UserPoints` (Id, UserId, CurrentPoints)。
  * **实体:** `PointTransaction` (Id, UserId, PointChangeType-Enum, ChangeAmount, RelatedEntityId, Description, TransactionTime)。
  * **仓储接口:** `IUserPointsRepository`, `IPointTransactionRepository`。
  * **领域服务:** `PointCalculator` (封装积分增减的业务规则)。
  * **领域事件处理器:** 监听以下领域事件，并触发积分变动：
    * `PostCreatedEvent`: 发帖成功，增加N积分。
    * `PostLikedEvent`: 帖子被点赞，增加M积分 (给帖子作者)。
    * `CommentAcceptedEvent` (自定义事件): 回帖被帖子作者采纳，增加L积分 (给回帖作者)。
    * `CommentLikedEvent`: 评论被点赞，增加K积分 (给评论作者)。
  * **应用服务:** `PointAppService` (获取用户当前积分、积分历史记录)。
  * **Admin UI (Angular):**
    * 用户积分管理：查询、手动调整用户积分、查看积分交易记录。
  * **Frontend UI (React):**
    * 个人中心显示当前积分。
    * 积分历史记录页面。

#### 4.2 商城系统

* **后端 (ABP - DDD):**
  * **聚合根:** `ShopItem` (Id, Name, Description, Type-Enum: AvatarFrame/NicknameColor/SignatureTail, Price-Points, IsActive, ImageUrl, EffectData-JSONB/string)。
  * **聚合根:** `UserInventory` (Id, UserId, ShopItemId, PurchaseTime, IsActive, ExpiryDate-可选, EffectData-JSONB/string)。
  * **仓储接口:** `IShopItemRepository`, `IUserInventoryRepository`。
  * **领域服务:** `ShopManager` (处理商品购买、用户物品激活/禁用)。
  * **应用服务:** `ShopAppService` (获取商品列表、购买商品、激活/禁用物品、获取用户库存)。
    * 购买商品时，调用 `PointCalculator` 扣除积分。
  * **Admin UI (Angular):**
    * 商品管理：创建/编辑/删除商品、设置价格、上下架、库存管理、查看商品销售记录。
    * 用户库存管理：查询用户已购物品、手动增减用户库存。
  * **Frontend UI (React):**
    * **商城页面:** 展示所有可购买的商品列表 (包括图片、名称、描述、价格)。
    * **商品详情页:** 查看商品详细信息。
    * **购买功能:** 用户点击购买后，后端扣除积分并添加到用户库存。
    * **我的物品/库存页面:** 显示用户已购买的物品。
    * **激活/禁用功能:** 用户可以选择激活某个头像框、昵称颜色或签名小尾巴。
    * **效果应用:** 前端需要根据用户激活的物品，动态地渲染UI。
      * **头像框:** 在用户头像周围添加CSS边框。
      * **昵称颜色:** 给用户昵称元素添加特定CSS样式。
      * **定制签名框小尾巴:** 在用户发帖/回帖时，在签名档末尾添加对应文本或HTML片段。

### 5. Docker 支持 (一键启动)

#### 5.1 Dockerize 各个服务

* **ForumProject.Host (Backend API):**
  * `Dockerfile`：基于 .NET SDK 构建，然后基于 .NET Runtime 运行。
  * 监听端口：例如 8000。
* **MongoDB:**
  * 直接使用官方 `mongo` 镜像。
  * 需要持久化数据卷。
  * 监听端口：默认 27017。
* **ForumProject.DbMigrator:**
  * `Dockerfile`：基于 .NET SDK 构建。
  * 作为一次性服务 (entrypoint)。
  * **注意:** 确保在MongoDB和Backend API之前运行，完成初始化。
* **React Frontend UI:**
  * `Dockerfile`：构建 React 应用 (例如 `npm run build` 或 `vite build`)，然后使用 Nginx 镜像作为静态文件服务器。
  * 监听端口：例如 3000。
* **ABP Angular UI (Admin):**
  * `Dockerfile`：构建 Angular 应用，然后使用 Nginx 镜像作为静态文件服务器。
  * 监听端口：例如 4200。

#### 5.2 `docker-compose.yml` 配置

* **Services:** 定义 `mongodb`, `backend-api`, `db-migrator`, `react-frontend`, `angular-admin` 等服务。
* **Ports:** 映射容器端口到宿主机端口。
* **Volumes:** 为 MongoDB 配置数据持久化。
* **Networks:** 创建自定义网络，使所有服务可以在内部通过服务名相互通信。
* **Dependencies (`depends_on`):**
  * `db-migrator` 依赖 `mongodb`。
  * `backend-api` 依赖 `mongodb`。
  * `react-frontend` 和 `angular-admin` 依赖 `backend-api` (在启动时)。
* **Environment Variables:** 配置数据库连接字符串、JWT密钥等。
* **启动命令:** `docker-compose up --build -d`

### 6. 开发流程规划

#### 阶段 0: 环境搭建与项目初始化 (2-3 天)

1. **ABP项目初始化:**
    * 使用 ABP CLI 创建新项目：`abp new ForumProject -t tiered -csf -u angular --db-provider mongodb`
    * 验证 ABP Angular UI 能否启动并访问默认的身份和权限管理页面。
2. **React前端项目初始化:**
    * 在解决方案外部或单独文件夹中创建 React 项目：`npm create vite@latest react-frontend --template react-ts` (推荐使用 Vite)。
    * 配置 React 项目的开发服务器代理，使其能访问 ABP 后端 API (例如 `vite.config.ts` 中的 `server.proxy`)。
3. **Git 版本控制:** 初始化 Git 仓库，建立开发分支。
4. **MongoDB 环境:** 确保本地或Docker中有一个MongoDB实例可以访问。

#### 阶段 1: 后端 MVP 开发 (3-4 周)

1. **DDD 基础构建:**
    * 在 `ForumProject.Domain` 项目中，定义 `Category`, `Post`, `Comment` 聚合根/实体、值对象、仓储接口。
    * 在 `ForumProject.Domain.MongoDB` 中实现对应的仓储。
    * 在 `ForumProject.DbMigrator` 中添加 MongoDB 索引和初始数据 (例如默认版块)。
2. **应用服务实现:**
    * 在 `ForumProject.Application.Contracts` 中定义 `ICategoryAppService`, `IPostAppService`, `ICommentAppService` 接口及其 DTOs。
    * 在 `ForumProject.Application` 中实现这些应用服务，调用领域层逻辑。
3. **API 暴露与测试:**
    * 确保所有应用服务通过 `ForumProject.Host` 项目暴露为 RESTful API。
    * 通过 Swagger/Scalar 验证所有 API 接口的可用性和正确性。
    * **Admin UI 适配:** 确认 ABP Angular UI 能够管理 `Category` (如果需要自定义管理界面，则进行开发)。

#### 阶段 2: React 前端 MVP 开发 (3-4 周)

1. **认证与授权集成:**
    * 在 React 中实现登录/注册页面，与后端认证 API 交互，处理 JWT Token 的存储 (localStorage/cookie) 和刷新。
    * 实现全局的 `AuthContext` 或 Redux/Zustand 状态管理，存储用户认证信息和权限列表。
    * 实现路由守卫 (Route Guard) 保护需要认证的页面。
2. **通用 UI 组件开发:**
    * 布局组件 (Header, Footer, Navigation)。
    * 帖子卡片、评论卡片等可复用组件。
    * 富文本编辑器集成。
3. **核心页面开发:**
    * 首页/版块列表页。
    * 帖子列表页 (支持分页、筛选、排序)。
    * 帖子详情页 (展示帖子内容、评论列表、发表评论)。
    * 发布/编辑帖子页面。
    * 个人中心页面 (显示基础信息)。
    * 错误处理和加载状态管理。

#### 阶段 3: Docker 化与部署 (1 周)

1. **编写 Dockerfile:** 为 `ForumProject.Host`, `ForumProject.DbMigrator`, `react-frontend`, `angular-admin` 编写 Dockerfile。
2. **编写 docker-compose.yml:** 整合所有服务，配置网络、卷、端口映射和环境变量。
3. **本地测试:** `docker-compose up --build -d` 启动整个项目，验证所有服务正常运行，功能联调。
4. **文档:** 编写详细的 Docker 部署说明。

#### 阶段 4: 积分系统开发 (2-3 周)

1. **DDD 建模:** 定义 `UserPoints`, `PointTransaction` 聚合根/实体，仓储接口。
2. **领域事件与处理器:** 定义 `PostLikedEvent` 等领域事件，并创建对应的领域事件处理器来更新用户积分。
3. **应用服务与 API:** 实现 `PointAppService`，提供查询用户积分和积分历史的 API。
4. **Admin UI:** 在 ABP Angular UI 中添加用户积分管理界面。
5. **React UI:** 在个人中心和相关页面展示用户积分和积分历史。

#### 阶段 5: 商城系统开发 (3-4 周)

1. **DDD 建模:** 定义 `ShopItem`, `UserInventory` 聚合根/实体，仓储接口。
2. **领域服务与应用服务:** 实现 `ShopAppService`，处理商品管理、购买、用户物品激活/禁用逻辑。
3. **Admin UI:** 在 ABP Angular UI 中添加商品管理和用户库存管理界面。
4. **React UI:**
    * 创建商城页面，展示商品列表。
    * 实现商品购买流程。
    * 创建“我的物品”页面，展示用户已购物品。
    * 实现物品的激活/禁用功能，并动态应用前端样式 (例如，根据激活的头像框ID，加载并显示对应的CSS类或图片)。

#### 阶段 6: 持续优化与扩展 (持续进行)

1. **搜索与标签功能:**
    * 后端：为帖子添加全文搜索（MongoDB支持），为标签提供单独管理。
    * 前端：搜索框，标签云/标签列表。
2. **消息/通知系统:**
    * 后端：集成 SignalR，或基于领域事件实现站内信。
    * 前端：实时通知、消息中心。
3. **文件上传服务:**
    * 后端：集成文件存储 (本地文件系统、MinIO、云存储如阿里云OSS/AWS S3)。
    * 前端：头像上传、帖子图片上传。
4. **SEO 优化:**
    * 对于 React 前端，考虑使用 Next.js 进行 SSR (服务器端渲染) 或预渲染。
    * 生成 Sitemap，robots.txt。
5. **性能优化:**
    * 数据库索引优化。
    * 缓存机制 (Redis)。
    * API 响应速度优化。
6. **安全性增强:**
    * XSS/CSRF 防护。
    * 输入验证 (后端和前端)。
    * 限流、防刷。
7. **日志与监控:**
    * 利用 ABP 内置日志系统，集成 ELK 或 Grafana/Prometheus。
8. **国际化 (i18n):** 如果需要支持多语言。

### 7. 起步建议

1. **按照“阶段 0”完成项目初始化。** 这是构建一切的基础。
2. **专注于后端 DDD 核心：** 先在 `ForumProject.Domain` 中清晰地定义 `Category`, `Post`, `Comment` 的聚合根、实体、值对象和它们之间的关系，以及仓储接口。这是业务逻辑的骨架。
3. **MongoDB 配置：** 确保 `ForumProject.Domain.MongoDB` 正确连接到你的 MongoDB 实例，并能实现基本实体的增删改查。
4. **自底向上验证：**
    * 先通过单元测试验证领域层逻辑。
    * 然后通过集成测试或 Swagger/Scalar 验证应用服务和 API。
    * 最后才进入前端 UI 的开发。
5. **MVP 优先原则：** 严格遵循MVP的核心功能列表，不要一开始就追求完美或所有附加功能。先让核心论坛功能跑起来，再迭代。
6. **持续集成/部署 (CI/CD):** 尽早设置基本的 CI/CD 管道，尤其是在 Docker 化之后，能够自动化构建、测试和部署。

这份大纲旨在提供一个清晰的开发路径和结构。在实际开发中，可能会根据具体情况和团队反馈进行调整。祝你项目顺利！
