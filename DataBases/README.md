# DataBases

该目录用于存放 Radish 本地开发环境数据库文件与相关数据目录。

## 目录说明

- `Radish.db`：业务主库（SQLite，本地默认）。
- `Radish.Log.db`：日志库（SQLite，本地默认）。
- `Radish.OpenIddict.db`：认证服务（OpenIddict）相关数据。
- `Radish.Message.db`：消息相关数据。
- `Radish.Hangfire.db`：任务调度（Hangfire）数据。
- `BackUp/`：数据库备份目录。
- `Temp/`：临时数据目录。
- `Uploads/`：上传文件目录。

## 使用建议

- 本地调试可直接使用 SQLite 文件。
- 切换 PostgreSQL 后，SQLite 文件可仅作为本地调试保留。
- 提交前请确认未误提交敏感或临时数据文件。
