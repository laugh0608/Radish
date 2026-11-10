# NoDrive / Valkey 隔离验证命令附录

> **已连同[验证方案](./nodrive-valkey-isolation-validation-plan.md)获批，以 `77549e95` 先推送后执行，并完成清理。** 实际结果、夹具与过程修正见[验证记录](../records/nodrive-valkey-validation-2026-10-21.md)。下文保留获批命令清单，不是可盲跑的一键脚本；方案提交时新增夹具尚不存在，执行版保存在记录指向的 evidence。
>
> 执行时使用一个专用 Bash 会话保留变量、函数和后台测试 PID，每阶段保存输出及退出码；出错进入第 7 节清理，不继续掩盖失败。主仓库的依赖、代码和部署配置不改。

## 1. 预检与固定输入

只使用本机 Docker daemon；执行 `docker context inspect` 后确认 endpoint 为本机 Unix socket。这里的版本和 digest 不自动升级。检查磁盘、端口及 Docker 可用性后才创建资源。

```bash
set -euo pipefail
RADISH_SOURCE=/Users/luobo/Code/Radish
RADISH_LAB_REF=47bdbfd5b225f5f3d809c2283686d7a80f6d154b
git -C "$RADISH_SOURCE" cat-file -e "$RADISH_LAB_REF^{commit}"
docker context inspect
docker info --format '{{.OSType}} {{.Architecture}}'
dotnet --version
df -h /private/tmp
lsof -nP -iTCP:15432 -iTCP:16379 -iTCP:16380 -iTCP:16381 -sTCP:LISTEN || true
```

`lsof` 仅用于显示检查结果；有任何目标端口占用即停止，不能因命令末尾 `|| true` 继续启动。还须检查 Docker 已发布端口（`docker ps --format '{{.Names}} {{.Ports}}'`），启动时若竞争占用则停止。SDK 需为已核对的 10.0.300，主机需为 arm64；发生差异先重新评估。

```bash
RADISH_LAB_ROOT=$(mktemp -d /private/tmp/radish-deplab.XXXXXX)
RADISH_LAB_RUN_ID=$(basename "$RADISH_LAB_ROOT")
RADISH_LAB_NAME="radish-${RADISH_LAB_RUN_ID}"
RADISH_LAB_OWNER="${RADISH_LAB_REF}:${RADISH_LAB_RUN_ID}"
RADISH_LAB_PG_PASS=radish-lab-only-pg-47bdbfd5
RADISH_LAB_KV_PASS=radish-lab-only-kv-47bdbfd5
RADISH_LAB_PG17='postgres@sha256:a63c1ea3f6a3a5a1621f97d2aba3b30faecfab4224c1b0000cc85ced57bb1939'
RADISH_LAB_PG16='postgres@sha256:a902560563d3a5661228585f53474c5065e7f273b21ffd77e555a8b5682c5690'
RADISH_LAB_REDIS='redis@sha256:44960b3100c48159a66d28c18b0588c193492e25f7551d60e51dd464fc3eca5b'
RADISH_LAB_VALKEY='valkey/valkey@sha256:418652cfb58ef879d4978c33553735d7147016032d5aefaa14c828e611eb9dfd'
mkdir -p "$RADISH_LAB_ROOT/evidence" "$RADISH_LAB_ROOT/packages" "$RADISH_LAB_ROOT/control"
printf '%s\n' "$RADISH_LAB_OWNER" > "$RADISH_LAB_ROOT/.owner"
export RADISH_LAB_RUN_ID
export NUGET_HTTP_CACHE_PATH="$RADISH_LAB_ROOT/http-cache"
export DOTNET_CLI_HOME="$RADISH_LAB_ROOT/dotnet-home"
export DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE=false
```

在任何包安装或容器创建前，先注册退出清理；这样 `set -e` 中断时也会尝试清理，并保留原失败退出码。归属或 Docker 可用性检查失败时保留资源并报告，不强删。

```bash
lab_cleanup() {
  local cleanup_failed=0 suffix container_name volume_name actual_owner remaining_containers remaining_volumes
  case "$RADISH_LAB_ROOT" in
    /private/tmp/radish-deplab.*) ;;
    *) return 1 ;;
  esac
  test -d "$RADISH_LAB_ROOT/evidence" || return 1
  test "$(cat "$RADISH_LAB_ROOT/.owner")" = "$RADISH_LAB_OWNER" || return 1
  if [ -n "${RADISH_LAB_TEST_PID:-}" ]; then
    kill -TERM "$RADISH_LAB_TEST_PID" 2>/dev/null || true
    wait "$RADISH_LAB_TEST_PID" 2>/dev/null || true
    unset RADISH_LAB_TEST_PID
  fi
  if ! docker info >/dev/null; then
    printf '%s\n' 'Docker 不可用，保留实验目录，需报告未清理资源。' >&2
    return 1
  fi
  for suffix in pg17 pg16 redis valkey valkey-rdb; do
    container_name="$RADISH_LAB_NAME-$suffix"
    if docker container inspect "$container_name" >/dev/null 2>&1; then
      actual_owner=$(docker inspect --format '{{index .Config.Labels "io.radish.deplab"}}' "$container_name")
      if [ "$actual_owner" != "$RADISH_LAB_OWNER" ]; then
        printf '%s\n' "拒绝清理归属不符的容器：$container_name" >&2
        cleanup_failed=1
        continue
      fi
      docker logs "$container_name" > "$RADISH_LAB_ROOT/evidence/container-$suffix.log" 2>&1 || cleanup_failed=1
      docker rm -f "$container_name" || cleanup_failed=1
    fi
  done
  for suffix in pg17data pg16data redisdata valkeydata valkey-rdbdata; do
    volume_name="$RADISH_LAB_NAME-$suffix"
    if docker volume inspect "$volume_name" >/dev/null 2>&1; then
      actual_owner=$(docker volume inspect --format '{{index .Labels "io.radish.deplab"}}' "$volume_name")
      if [ "$actual_owner" != "$RADISH_LAB_OWNER" ]; then
        printf '%s\n' "拒绝清理归属不符的卷：$volume_name" >&2
        cleanup_failed=1
        continue
      fi
      docker volume rm "$volume_name" || cleanup_failed=1
    fi
  done
  if [ "$cleanup_failed" -ne 0 ]; then return 1; fi
  remaining_containers=$(docker ps -aq --filter "label=io.radish.deplab=$RADISH_LAB_OWNER") || return 1
  remaining_volumes=$(docker volume ls -q --filter "label=io.radish.deplab=$RADISH_LAB_OWNER") || return 1
  test -z "$remaining_containers" && test -z "$remaining_volumes" || return 1
  rm -rf -- "$RADISH_LAB_ROOT/src" "$RADISH_LAB_ROOT/packages" \
    "$RADISH_LAB_ROOT/publish" "$RADISH_LAB_ROOT/http-cache" \
    "$RADISH_LAB_ROOT/dotnet-home" "$RADISH_LAB_ROOT/control" || return 1
  docker ps -a --filter "label=io.radish.deplab=$RADISH_LAB_OWNER"
  docker volume ls --filter "label=io.radish.deplab=$RADISH_LAB_OWNER"
  git -C "$RADISH_SOURCE" status --short
}
trap 'lab_exit_code=$?; trap - EXIT; set +e; lab_cleanup; lab_cleanup_code=$?; if [ "$lab_exit_code" -ne 0 ]; then exit "$lab_exit_code"; fi; exit "$lab_cleanup_code"' EXIT
```

测试密码为本方案公开的合成凭据，不可用于任何部署。上述关闭配置文件 reload 仅限测试进程，用于规避本机已定位的文件监听回调问题，不修改产品配置。记录开始时已有镜像清单，实验结束保留 Docker 镜像缓存，不全局删除镜像。

## 2. 导出源码、准备原生产物、修改副本引用

```bash
for variant in baseline drivers nodrive; do
  mkdir -p "$RADISH_LAB_ROOT/src/$variant"
  git -C "$RADISH_SOURCE" archive "$RADISH_LAB_REF" |
    tar -x -C "$RADISH_LAB_ROOT/src/$variant"
  mkdir -p "$RADISH_LAB_ROOT/src/$variant/artifacts/native"
  cp -R "$RADISH_SOURCE/artifacts/native/osx-arm64" \
    "$RADISH_LAB_ROOT/src/$variant/artifacts/native/"
  (
    cd "$RADISH_LAB_ROOT/src/$variant"
    node Scripts/build-native-images.mjs --verify --rid=osx-arm64 --debug
    node Scripts/build-native-images.mjs --verify --rid=osx-arm64
  )
done

for variant in drivers nodrive; do
  dotnet add "$RADISH_LAB_ROOT/src/$variant/Radish.Common/Radish.Common.csproj" \
    package Npgsql --version '[10.0.0]' --no-restore
  dotnet add "$RADISH_LAB_ROOT/src/$variant/Radish.Common/Radish.Common.csproj" \
    package Microsoft.Data.Sqlite --version '[10.0.0]' --no-restore
done

for project in Radish.Common Radish.Infrastructure; do
  dotnet remove "$RADISH_LAB_ROOT/src/nodrive/$project/$project.csproj" package SqlSugarCore
  dotnet add "$RADISH_LAB_ROOT/src/nodrive/$project/$project.csproj" \
    package SqlSugarCoreNoDrive --version '[5.1.4.207]' --no-restore
done
dotnet add "$RADISH_LAB_ROOT/src/nodrive/Radish.Common/Radish.Common.csproj" \
  package SystemCommon --version '[8.5.0]' --no-restore
```

方括号使用 NuGet 精确版本范围，防止候选直接引用被自动抬高。包级改动完成后保存 csproj 差异；先实现方案指定的 DriverProbe 夹具并等量加入三组，不修改生产方法。所有目录与主工作区相互独立，不共享 bin / obj。

```bash
for variant in baseline drivers nodrive; do
  mkdir -p "$RADISH_LAB_ROOT/evidence/$variant"
  (
    cd "$RADISH_LAB_ROOT/src/$variant"
    dotnet restore Radish.slnx --disable-parallel --use-lock-file --force-evaluate \
      --packages "$RADISH_LAB_ROOT/packages"
    dotnet package list --project Radish.slnx --vulnerable --include-transitive \
      --format json --no-restore > "$RADISH_LAB_ROOT/evidence/$variant/nuget-audit.json"
  )
done
```

此处先检查三组 assets / lock / 审计报告。审计命令退出 0 不代表没有漏洞，需按仓库 High / Critical 策略解析 findings。依赖图超出方案范围、出现降级或需额外包时停止，不能继续编译来掩盖依赖问题。

```bash
for variant in baseline drivers nodrive; do
  (
    cd "$RADISH_LAB_ROOT/src/$variant"
    dotnet restore Radish.slnx --locked-mode --disable-parallel \
      --packages "$RADISH_LAB_ROOT/packages"
    dotnet build Radish.slnx -c Debug --no-restore --warnaserror
    dotnet build Radish.slnx -c Release --no-restore --warnaserror
  )
done
```

保存完整输出和退出码；本附录省略重复重定向。普通 bin 仍在各副本目录内，确保源文件定位测试有效。

## 3. PostgreSQL 启动与 NoDrive 测试

定义资源归属检查和有限就绪等待。资源名若已存在直接停止，不复用，即使标签碰巧相同也不删后重建。

```bash
lab_wait_healthy() {
  local container_name="$1"
  local attempt
  for attempt in $(seq 1 60); do
    if [ "$(docker inspect --format '{{.State.Health.Status}}' "$container_name")" = healthy ]; then
      return 0
    fi
    sleep 1
  done
  docker logs --tail 80 "$container_name"
  return 1
}

lab_remove_container() {
  local container_name="$1"
  test "$(docker inspect --format '{{index .Config.Labels "io.radish.deplab"}}' "$container_name")" = "$RADISH_LAB_OWNER"
  docker rm -f "$container_name"
}

lab_start_pg() {
  local pg_version="$1"
  local pg_image="$2"
  local container_name="$RADISH_LAB_NAME-pg$pg_version"
  local volume_name="$RADISH_LAB_NAME-pg${pg_version}data"
  if docker container inspect "$container_name" >/dev/null 2>&1 ||
     docker volume inspect "$volume_name" >/dev/null 2>&1; then
    return 1
  fi
  docker volume create --label "io.radish.deplab=$RADISH_LAB_OWNER" "$volume_name"
  docker run -d --pull=never --platform linux/arm64 --name "$container_name" \
    --label "io.radish.deplab=$RADISH_LAB_OWNER" \
    --cpus=2 --memory=1g --pids-limit=256 --shm-size=256m \
    -p 127.0.0.1:15432:5432 \
    --mount "type=volume,source=$volume_name,target=/var/lib/postgresql/data" \
    -e POSTGRES_USER=radish_lab -e POSTGRES_DB=radish_lab \
    -e "POSTGRES_PASSWORD=$RADISH_LAB_PG_PASS" \
    --health-cmd='pg_isready -U radish_lab -d radish_lab' \
    --health-interval=2s --health-timeout=3s --health-retries=30 "$pg_image"
  lab_wait_healthy "$container_name"
  docker exec "$container_name" psql -U radish_lab -d radish_lab -Atc 'SHOW server_version'
}

docker pull --platform linux/arm64 "$RADISH_LAB_PG17"
docker pull --platform linux/arm64 "$RADISH_LAB_PG16"
lab_start_pg 17 "$RADISH_LAB_PG17"
export RADISH_TEST_POSTGRES_CONNECTION_STRING="Host=127.0.0.1;Port=15432;Database=radish_lab;Username=radish_lab;Password=$RADISH_LAB_PG_PASS;Pooling=false"

for variant in baseline drivers nodrive; do
  (
    cd "$RADISH_LAB_ROOT/src/$variant"
    dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj -c Debug --no-build --no-restore \
      --logger 'trx;LogFileName=full-pg17.trx' \
      --results-directory "$RADISH_LAB_ROOT/evidence/$variant/pg17"
  )
done
lab_remove_container "$RADISH_LAB_NAME-pg17"
lab_start_pg 16 "$RADISH_LAB_PG16"
for variant in baseline drivers nodrive; do
  (
    cd "$RADISH_LAB_ROOT/src/$variant"
    dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj -c Debug --no-build --no-restore \
      --filter 'Database=PostgreSQL' --logger 'trx;LogFileName=postgres16.trx' \
      --results-directory "$RADISH_LAB_ROOT/evidence/$variant/pg16"
  )
done
lab_remove_container "$RADISH_LAB_NAME-pg16"
unset RADISH_TEST_POSTGRES_CONNECTION_STRING
```

检验 TRX 中原有 42 个 PostgreSQL 用例实际运行，新增 DriverProbe 数量单独列出；不能仅看命令退出码。全量测试沿用 macOS 证书夹具，若沙盒阻止创建临时钥匙串，按仓库规则仅对测试命令提权，不调整系统证书信任。

四宿主本地产物生成与审核：

```bash
for variant in baseline drivers nodrive; do
  for project in Radish.Api Radish.Auth Radish.Gateway Radish.DbMigrate; do
    (
      cd "$RADISH_LAB_ROOT/src/$variant"
      dotnet publish "$project/$project.csproj" -c Release --no-build --no-restore \
        -o "$RADISH_LAB_ROOT/publish/$variant/$project"
    )
  done
done
```

使用只读脚本生成全部 assets / deps 包清单、publish 文件路径 / SHA256 和 A→B / B→C 差异；核对最终 SqlSugar.dll、驱动实际版本及应退出的包，不删除或改写任何 publish 文件。publish 仅是本地产物，不上传或部署。

## 4. Redis / Valkey 启动与业务对照

NoDrive 步骤完成并停止 PostgreSQL 后执行。获批后在 baseline 副本实现方案列出的三类 Redis 测试；测试新增后先锁定还原并构建，测试框架与客户端版本不变。

```bash
docker pull --platform linux/arm64 "$RADISH_LAB_REDIS"
docker pull --platform linux/arm64 "$RADISH_LAB_VALKEY"
for suffix in redis valkey valkey-rdb; do
  if docker container inspect "$RADISH_LAB_NAME-$suffix" >/dev/null 2>&1 ||
     docker volume inspect "$RADISH_LAB_NAME-${suffix}data" >/dev/null 2>&1; then
    exit 1
  fi
  docker volume create --label "io.radish.deplab=$RADISH_LAB_OWNER" \
    "$RADISH_LAB_NAME-${suffix}data"
done

docker run -d --pull=never --platform linux/arm64 --name "$RADISH_LAB_NAME-redis" \
  --label "io.radish.deplab=$RADISH_LAB_OWNER" --cpus=1 --memory=512m --pids-limit=128 \
  -p 127.0.0.1:16379:6379 \
  --mount "type=volume,source=$RADISH_LAB_NAME-redisdata,target=/data" \
  -e "REDISCLI_AUTH=$RADISH_LAB_KV_PASS" \
  --health-cmd='redis-cli ping | grep -q PONG' \
  --health-interval=2s --health-timeout=3s --health-retries=30 \
  "$RADISH_LAB_REDIS" redis-server --appendonly yes --appendfsync everysec \
  --save '' --requirepass "$RADISH_LAB_KV_PASS"

docker run -d --pull=never --platform linux/arm64 --name "$RADISH_LAB_NAME-valkey" \
  --label "io.radish.deplab=$RADISH_LAB_OWNER" --cpus=1 --memory=512m --pids-limit=128 \
  -p 127.0.0.1:16380:6379 \
  --mount "type=volume,source=$RADISH_LAB_NAME-valkeydata,target=/data" \
  -e "REDISCLI_AUTH=$RADISH_LAB_KV_PASS" \
  --health-cmd='valkey-cli ping | grep -q PONG' \
  --health-interval=2s --health-timeout=3s --health-retries=30 \
  "$RADISH_LAB_VALKEY" valkey-server --appendonly yes --appendfsync everysec \
  --save '' --requirepass "$RADISH_LAB_KV_PASS"

lab_wait_healthy "$RADISH_LAB_NAME-redis"
lab_wait_healthy "$RADISH_LAB_NAME-valkey"
docker exec "$RADISH_LAB_NAME-redis" redis-cli INFO server
docker exec "$RADISH_LAB_NAME-valkey" valkey-cli INFO server

export RADISH_LAB_REDIS_CONNECTION_STRING="127.0.0.1:16379,password=$RADISH_LAB_KV_PASS,defaultDatabase=15,abortConnect=false,connectTimeout=3000,syncTimeout=3000"
export RADISH_LAB_VALKEY_CONNECTION_STRING="127.0.0.1:16380,password=$RADISH_LAB_KV_PASS,defaultDatabase=15,abortConnect=false,connectTimeout=3000,syncTimeout=3000"
export RADISH_LAB_KEY_PREFIX="${RADISH_LAB_RUN_ID}:"
(
  cd "$RADISH_LAB_ROOT/src/baseline"
  dotnet restore Radish.slnx --locked-mode --disable-parallel --packages "$RADISH_LAB_ROOT/packages"
  dotnet build Radish.Api.Tests/Radish.Api.Tests.csproj -c Debug --no-restore --warnaserror
  dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj -c Debug --no-build --no-restore \
    --filter 'FullyQualifiedName~DependencyLabRedisContractTests' \
    --logger 'trx;LogFileName=redis-valkey-contract.trx' \
    --results-directory "$RADISH_LAB_ROOT/evidence/redis-valkey"
)
```

测试内创建 / 删除限定实验前缀的 ACL 用户，密码为单独合成测试值；所有管理动作只针对本轮两个容器。测试执行前确认 filter 非空，执行后逐项检查 Redis / Valkey 各自用例和比较结果。不会用新增测试覆盖或修改原生产 Lua。

## 5. AOF 与 Valkey RDB 恢复

Persistence 夹具拟提供 `seed / verify-restart / verify-rdb` 三个阶段，保留合成预期值与 UTC 写入时间到 `evidence/`，业务 key TTL 设为足够覆盖恢复过程的 30 分钟；另有短 TTL 用例独立执行。这是待实现的测试接口，不是已存在能力。

```bash
lab_wait_persistence() {
  local container_name="$1"
  local cli_name="$2"
  local progress_field="$3"
  local status_field="$4"
  local attempt info progress status
  for attempt in $(seq 1 60); do
    info=$(docker exec "$container_name" "$cli_name" --raw INFO persistence | tr -d '\r')
    progress=$(printf '%s\n' "$info" | awk -F: -v key="$progress_field" '$1 == key { print $2 }')
    status=$(printf '%s\n' "$info" | awk -F: -v key="$status_field" '$1 == key { print $2 }')
    if [ "$progress" = 0 ]; then test "$status" = ok; return; fi
    sleep 1
  done
  return 1
}

export RADISH_LAB_PERSISTENCE_STATE="$RADISH_LAB_ROOT/evidence/persistence-state.json"
(
  cd "$RADISH_LAB_ROOT/src/baseline"
  RADISH_LAB_PERSISTENCE_PHASE=seed dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj \
    --no-build --no-restore --filter 'FullyQualifiedName~DependencyLabRedisPersistenceTests'
)
docker exec "$RADISH_LAB_NAME-redis" redis-cli -e BGREWRITEAOF
lab_wait_persistence "$RADISH_LAB_NAME-redis" redis-cli aof_rewrite_in_progress aof_last_bgrewrite_status
docker exec "$RADISH_LAB_NAME-valkey" valkey-cli -e BGREWRITEAOF
lab_wait_persistence "$RADISH_LAB_NAME-valkey" valkey-cli aof_rewrite_in_progress aof_last_bgrewrite_status
docker exec "$RADISH_LAB_NAME-valkey" valkey-cli -e BGSAVE
lab_wait_persistence "$RADISH_LAB_NAME-valkey" valkey-cli rdb_bgsave_in_progress rdb_last_bgsave_status
```

每条后台持久化命令必须等前一条完成再发下一条。以 `INFO persistence` 检查对应 `*_in_progress=0` 且 `*_last_*_status=ok`；每项最多等待 60 秒，失败即停止并保留日志，不能只凭返回“started”继续。AOF rewrite 会产生自己的 base 文件，复制 RDB 时明确只取 `dump.rdb`。

```bash
docker exec "$RADISH_LAB_NAME-redis" redis-cli INFO persistence
docker exec "$RADISH_LAB_NAME-valkey" valkey-cli INFO persistence
docker cp "$RADISH_LAB_NAME-valkey:/data/dump.rdb" "$RADISH_LAB_ROOT/evidence/valkey-dump.rdb"
docker restart --time 10 "$RADISH_LAB_NAME-redis" "$RADISH_LAB_NAME-valkey"
lab_wait_healthy "$RADISH_LAB_NAME-redis"
lab_wait_healthy "$RADISH_LAB_NAME-valkey"
(
  cd "$RADISH_LAB_ROOT/src/baseline"
  RADISH_LAB_PERSISTENCE_PHASE=verify-restart dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj \
    --no-build --no-restore --filter 'FullyQualifiedName~DependencyLabRedisPersistenceTests'
)

docker create --pull=never --platform linux/arm64 --name "$RADISH_LAB_NAME-valkey-rdb" \
  --label "io.radish.deplab=$RADISH_LAB_OWNER" --cpus=1 --memory=512m --pids-limit=128 \
  -p 127.0.0.1:16381:6379 \
  --mount "type=volume,source=$RADISH_LAB_NAME-valkey-rdbdata,target=/data" \
  -e "REDISCLI_AUTH=$RADISH_LAB_KV_PASS" \
  --health-cmd='valkey-cli ping | grep -q PONG' \
  --health-interval=2s --health-timeout=3s --health-retries=30 \
  "$RADISH_LAB_VALKEY" valkey-server --appendonly no --save '' --requirepass "$RADISH_LAB_KV_PASS"
docker cp "$RADISH_LAB_ROOT/evidence/valkey-dump.rdb" "$RADISH_LAB_NAME-valkey-rdb:/data/dump.rdb"
docker start "$RADISH_LAB_NAME-valkey-rdb"
lab_wait_healthy "$RADISH_LAB_NAME-valkey-rdb"
export RADISH_LAB_RDB_CONNECTION_STRING="127.0.0.1:16381,password=$RADISH_LAB_KV_PASS,defaultDatabase=15,abortConnect=false"
(
  cd "$RADISH_LAB_ROOT/src/baseline"
  RADISH_LAB_PERSISTENCE_PHASE=verify-rdb dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj \
    --no-build --no-restore --filter 'FullyQualifiedName~DependencyLabRedisPersistenceTests'
)
```

本阶段验证正常重启和同版本 RDB 恢复，不宣称已验证断电零丢失。不会把 Redis 7.4 的 RDB / AOF 交给 Valkey，也不向源服务写回恢复数据。[迁移边界](https://valkey.io/topics/migration/)与[持久化机制](https://valkey.io/topics/persistence/)来自 Valkey 官方文档。

## 6. 保持同一客户端的断线 / 重连

Recovery 夹具后台运行，使用同一个 multiplexer；按真实断线 / 重连观察写出 `connected`、`disconnected` 信号文件，绝不能由操作者伪造信号。每次只操作指定测试容器，先保存后台 PID；异常时清理该 PID 后清理容器。

```bash
lab_wait_signal() {
  local signal_path="$1"
  local attempt
  for attempt in $(seq 1 60); do
    if [ -f "$signal_path" ]; then return 0; fi
    sleep 1
  done
  return 1
}

for target in redis valkey; do
  mkdir -p "$RADISH_LAB_ROOT/control/$target"
  (
    cd "$RADISH_LAB_ROOT/src/baseline"
    exec env RADISH_LAB_RECOVERY_TARGET="$target" \
    RADISH_LAB_RECOVERY_SIGNALS="$RADISH_LAB_ROOT/control/$target" \
      dotnet test Radish.Api.Tests/Radish.Api.Tests.csproj --no-build --no-restore \
      --filter 'FullyQualifiedName~DependencyLabRedisRecoveryTests' \
      --logger "trx;LogFileName=recovery-$target.trx" \
      --results-directory "$RADISH_LAB_ROOT/evidence/redis-valkey" \
      > "$RADISH_LAB_ROOT/evidence/recovery-$target.log" 2>&1
  ) &
  RADISH_LAB_TEST_PID=$!
  lab_wait_signal "$RADISH_LAB_ROOT/control/$target/connected"
  docker stop --time 10 "$RADISH_LAB_NAME-$target"
  lab_wait_signal "$RADISH_LAB_ROOT/control/$target/disconnected"
  docker start "$RADISH_LAB_NAME-$target"
  lab_wait_healthy "$RADISH_LAB_NAME-$target"
  wait "$RADISH_LAB_TEST_PID"
  unset RADISH_LAB_TEST_PID
done
```

在重启阶段只对实验容器进行中断，不借用项目服务制造断线。测试设有界超时，并要求错误被观测、重连后重新验证原 key 的值与 TTL；结果不能等同网络不确定写入的 exactly-once 保证。

## 7. 清理与最终核对

正常结束或失败均执行。清理前将测试补丁、lock / assets 摘要、包与文件清单、SQLSugar.dll 哈希、TRX、失败日志保存到 `evidence/`；Docker 不可用时明确报告未清理资源，不假装成功。

```bash
# 已在第 1 节定义 lab_cleanup 并注册失败时 EXIT 清理。
set +e
lab_cleanup
RADISH_LAB_CLEANUP_EXIT=$?
trap - EXIT
exit "$RADISH_LAB_CLEANUP_EXIT"
```

容器和卷筛选应为空，原工作区没有实验性代码 / 依赖 / 配置变化。`evidence/`、`.owner` 保留为可追溯证据；关闭专用 Bash 会话释放其余环境变量。镜像缓存有意保留，不运行 `docker system prune`、`docker volume prune` 或删除既有 NuGet 缓存。

Docker 命令语义参考[官方容器运行文档](https://docs.docker.com/engine/containers/run/)；正式执行前仍需完成方案中的批准与本机预检。
