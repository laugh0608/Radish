/** 隔离实验配置，不是 production compose；HTTP 和 metrics 都仅在测试网络内。 */
export function boundaryConfig(host) {
  if (!/^[a-z0-9-]+$/u.test(host)) throw new Error('Invalid probe host.');
  return `[SERVICE]
    Flush 1
    Grace 2
    Log_Level error
    Parsers_File /probe/parsers.conf
    storage.path /probe/buffer
    storage.sync full
    storage.checksum on
    storage.backlog.mem_limit 2M
    storage.metrics on
    HTTP_Server on
    HTTP_Listen 0.0.0.0
    HTTP_Port 2020
    scheduler.base 1
    scheduler.cap 3
[INPUT]
    Name forward
    Alias guarded_input
    Listen 0.0.0.0
    Port 24224
    Buffer_Chunk_Size 64K
    Buffer_Max_Size 64K
    storage.type filesystem
[FILTER]
    Name lua
    Match *
    script /probe/normalize.lua
    call preflight
    protected_mode false
[FILTER]
    Name parser
    Match *
    Key_Name log
    Parser event
    Reserve_Data true
    Preserve_Key true
[FILTER]
    Name lua
    Match *
    script /probe/normalize.lua
    call normalize
    protected_mode false
[FILTER]
    Name record_modifier
    Match *
    Uuid_Key eventId
[FILTER]
    Name lua
    Match *
    script /probe/normalize.lua
    call finalize
    protected_mode false
[OUTPUT]
    Name file
    Alias runtime_file
    Match *
    Path /probe/files
    File runtime.jsonl
    Format plain
    rotate true
    rotate_max_size 1M
    rotate_max_files 3
    rotate_gzip false
    Retry_Limit false
    storage.total_limit_size 4M
[OUTPUT]
    Name http
    Alias runtime_http
    Match *
    Host ${host}
    Port 8080
    URI /internal/logs/ingest
    Format json_lines
    Json_date_key false
    log_response_payload false
    Retry_Limit false
    storage.total_limit_size 4M
`;
}
