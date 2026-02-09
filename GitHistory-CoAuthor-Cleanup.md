# Git 历史协作者信息清理指南

本文用于说明：当历史提交中误写了 `Co-Authored-By`，导致 GitHub Contributors 出现错误协作者时，为什么需要重写历史，以及如何以相对安全的方式处理。

## 背景与原因

### 问题现象

- 某次提交（例如：`docs: 测试添加Codex协作`）包含了错误的协作者尾注：

```text
Co-Authored-By: GPT 5.2 Codex <noreply@openai.com>
```

- 该提交已进入受统计分支（`dev`，并且已 PR 到 `master`）。

### 为什么必须重写历史

- GitHub Contributors 统计基于分支历史中的提交作者/协作者信息。
- 仅新增一个“修复提交”或再次 PR，**不会移除历史里已经存在的错误 `Co-Authored-By`**。
- 要彻底消除影响，必须把目标提交的提交信息改写后，覆盖远端历史。

## 风险说明

- 这是 **历史重写** 操作，会改变提交哈希。
- 对共享分支（`dev`/`master`）执行后，团队成员本地分支都需要同步（`reset --hard` 或重建分支）。
- 如果分支保护开启了禁止强推，需要临时放开（建议仅管理员、短时开放）。

## 安全处理步骤（推荐）

> 建议在“新 clone”中执行，避免污染日常工作目录。

### 1) 准备与同步

```bash
git clone <repo-url> repo-fix-coauthor
cd repo-fix-coauthor
git fetch --all --prune
```

### 2) 定位问题提交（分别在 dev/master）

```bash
git log dev --grep="测试添加Codex协作" --oneline
git log master --grep="测试添加Codex协作" --oneline
```

> 注意：同一逻辑提交在 `master` 上哈希可能与 `dev` 不同（如 squash/rebase merge）。

### 3) 创建远端备份分支（强烈建议）

```bash
git branch backup/dev-before-fix origin/dev
git branch backup/master-before-fix origin/master
git push origin backup/dev-before-fix backup/master-before-fix
```

### 4) 重写 dev

```bash
git checkout dev
git pull --ff-only origin dev
git rebase -i <DEV_问题提交哈希>^
```

- 在交互列表里把该提交从 `pick` 改为 `reword`（或 `edit`）。
- 编辑提交信息，删除错误的 `Co-Authored-By` 行并保存。
- 如需继续：

```bash
git rebase --continue
```

### 5) 重写 master

```bash
git checkout master
git pull --ff-only origin master
git rebase -i <MASTER_问题提交哈希>^
```

- 同样删除错误 `Co-Authored-By` 行并完成 rebase。

### 6) 本地校验

```bash
git log dev --format='%H%n%B' | rg "Co-Authored-By|GPT 5.2 Codex"
git log master --format='%H%n%B' | rg "Co-Authored-By|GPT 5.2 Codex"
```

- 两条命令都应无输出。

### 7) 安全强推

```bash
git push origin dev --force-with-lease
git push origin master --force-with-lease
```

> 使用 `--force-with-lease`，不要用裸 `--force`。

### 8) 通知团队同步本地分支

```bash
git fetch origin
git checkout dev && git reset --hard origin/dev
git checkout master && git reset --hard origin/master
```

## 回滚方案（紧急）

若重写后发现异常，可用备份分支快速恢复：

```bash
git checkout dev
git reset --hard backup/dev-before-fix
git push origin dev --force-with-lease

git checkout master
git reset --hard backup/master-before-fix
git push origin master --force-with-lease
```

## 备注

- GitHub Contributors 页面可能存在缓存延迟，历史修正后不会立即刷新。
- 建议在低峰期执行，并在团队群里提前公告维护窗口。
