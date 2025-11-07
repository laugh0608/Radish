# Radish

## 备注

* 默认用户名: `admin`
* 默认密码: `1q2w3E*`

## About this solution

This is a layered startup solution based on [Domain Driven Design (DDD)](https://abp.io/docs/latest/framework/architecture/domain-driven-design) practises. All the fundamental ABP modules are already installed. Check the [Application Startup Template](https://abp.io/docs/latest/solution-templates/layered-web-application) documentation for more info.

### Pre-requirements

* [.NET9.0+ SDK](https://dotnet.microsoft.com/download/dotnet)
* [Node v18 or 20](https://nodejs.org/en)

### Configurations

The solution comes with a default configuration that works out of the box. However, you may consider to change the following configuration before running your solution:

* Check the `ConnectionStrings` in `appsettings.json` files under the `Radish.HttpApi.Host` and `Radish.DbMigrator` projects and change it if you need.

### 本地联调（重要）

- Host 以 HTTPS 提供 API：`https://localhost:44342`。首次开发请信任本机证书：`dotnet dev-certs https --trust`。
- 前端默认用 HTTP 启动：Angular `http://localhost:4200`，React `http://localhost:5173`。
- CORS 配置使用 `src/Radish.HttpApi.Host/.env` 的 `App__CorsOrigins`，建议同时写入 http 与 https，例如：
  - `App__CorsOrigins=http://localhost:4200,https://localhost:4200,http://localhost:5173,https://localhost:5173`
- Host 会自动为相同 host:port 补全另一种协议（只写 https 也会运行时放行 http）。
- 预检自测（期望 204 且包含 `Access-Control-Allow-Origin`）：
  - `curl -i -X OPTIONS "https://localhost:44342/api/abp/application-configuration" -H "Origin: http://localhost:4200" -H "Access-Control-Request-Method: GET"`

示例与更多细节：
- 后端：`docs/backend/README.md`
- Angular：`docs/frontend/angular/README.md`
- React：`docs/frontend/react/README.md`

### Before running the application

* Run `abp install-libs` command on your solution folder to install client-side package dependencies. This step is automatically done when you create a new solution, if you didn't especially disabled it. However, you should run it yourself if you have first cloned this solution from your source control, or added a new client-side package dependency to your solution.
* Run `Radish.DbMigrator` to create the initial database. This step is also automatically done when you create a new solution, if you didn't especially disabled it. This should be done in the first run. It is also needed if a new database migration is added to the solution later.

#### Generating a Signing Certificate

In the production environment, you need to use a production signing certificate. ABP Framework sets up signing and encryption certificates in your application and expects an `openiddict.pfx` file in your application.

To generate a signing certificate, you can use the following command:

```bash
dotnet dev-certs https -v -ep openiddict.pfx -p 83c828cf-930d-4c16-8cc7-e98e05fc8143
```

> `83c828cf-930d-4c16-8cc7-e98e05fc8143` is the password of the certificate, you can change it to any password you want.

It is recommended to use **two** RSA certificates, distinct from the certificate(s) used for HTTPS: one for encryption, one for signing.

For more information, please refer to: [OpenIddict Certificate Configuration](https://documentation.openiddict.com/configuration/encryption-and-signing-credentials.html#registering-a-certificate-recommended-for-production-ready-scenarios)

> Also, see the [Configuring OpenIddict](https://abp.io/docs/latest/Deployment/Configuring-OpenIddict#production-environment) documentation for more information.

### Solution structure

This is a layered monolith application that consists of the following applications:

* `Radish.DbMigrator`: A console application which applies the migrations and also seeds the initial data. It is useful on development as well as on production environment.
* `Radish.HttpApi.Host`: ASP.NET Core API application that is used to expose the APIs to the clients.
* `angular`: Angular application.


## Deploying the application

Deploying an ABP application follows the same process as deploying any .NET or ASP.NET Core application. However, there are important considerations to keep in mind. For detailed guidance, refer to ABP's [deployment documentation](https://abp.io/docs/latest/Deployment/Index).

### Additional resources


#### Internal Resources

项目组件文档（统一归档在 `docs/`）：

- 后端（.NET / ABP）：`docs/backend/README.md`
- 管理端（Angular）：`docs/frontend/angular/README.md`
- 前端 UI（React）：`docs/frontend/react/README.md`

#### External Resources
You can see the following resources to learn more about your solution and the ABP Framework:

* [Web Application Development Tutorial](https://abp.io/docs/latest/tutorials/book-store/part-1)
* [Application Startup Template](https://abp.io/docs/latest/startup-templates/application/index)
