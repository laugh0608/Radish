-- 本文件与生成的 policy.lua 同目录。实验配置须按 preflight/parser/normalize/uuid/finalize 排序。
local policy = dofile(assert(os.getenv('RADISH_LOG_POLICY_PATH'), 'Logging policy path is required'))
local deployment = assert(os.getenv('RADISH_LOG_DEPLOYMENT'), 'Logging deployment is required')
local release = assert(os.getenv('RADISH_LOG_RELEASE'), 'Logging release is required')
local mode = os.getenv('RADISH_LOG_MODE') or 'Production'
assert(mode == 'Production' or mode == 'Development', 'Invalid logging mode')
assert(#deployment <= 64 and deployment:match('^[%w][%w._-]*$'), 'Invalid deployment')
assert(#release <= 64 and release:match('^[%w][%w._-]*$'), 'Invalid release')

local function contains(values, value)
    for _, candidate in ipairs(values) do if candidate == value then return true end end
    return false
end

local function service_for(tag)
    local service, container = tag:match('^radish%.([%w-]+)%.([0-9a-f]+)$')
    if not container or #container ~= 64 then return nil end
    if contains(policy.services, service) then return service end
    return nil
end

local function uuid(value)
    return type(value) == 'string' and #value == 36
        and value:match('^%x%x%x%x%x%x%x%x%-%x%x%x%x%-%x%x%x%x%-%x%x%x%x%-%x%x%x%x%x%x%x%x%x%x%x%x$')
        and value == value:lower() and value:find('[1-9a-f]') ~= nil
end

local function utc(timestamp)
    return os.date('!%Y-%m-%dT%H:%M:%S', math.floor(timestamp)) .. '.000Z'
end

local function base(tag, timestamp, code, level)
    local container = service_for(tag) and tag:match('%.([0-9a-f]+)$') or nil
    return {
        schemaVersion = policy.schemaVersion, eventCode = code,
        occurredAtUtc = utc(timestamp), observedAtUtc = utc(timestamp),
        deploymentId = deployment, service = service_for(tag) or 'log-collector',
        instanceId = container or 'unknown', containerId = container, release = release, mode = mode,
        sourceCategory = 'pipeline', level = level, diagnostic = false, isFatal = false,
        message = policy.events[code], messageTemplate = policy.events[code],
        normalizationStatus = 'normalized', redacted = false, truncated = false, properties = {}
    }
end

local function rejected(tag, timestamp, reason)
    local output = base(tag, timestamp, 'pipeline.rejected', 'Warning')
    output.normalizationStatus = 'rejected'
    output.redacted = true
    output.properties = { rejectionReason = reason }
    return output
end

local function native(tag, raw)
    local service = service_for(tag)
    if service == 'postgres' then
        local severity = raw:match('^%d%d%d%d%-%d%d%-%d%d%s+[%d:.]+%s+[%w+:-]+%s+%[%d+%]%s+([%u%d]+):')
        if severity and severity:match('^DEBUG%d?$') then return { drop = true } end
        if severity == 'DETAIL' or severity == 'STATEMENT' or severity == 'CONTEXT' or severity == 'HINT' then return { drop = true } end
        local levels = { LOG = 'Info', INFO = 'Info', NOTICE = 'Info', WARNING = 'Warning', ERROR = 'Error', FATAL = 'Error', PANIC = 'Error' }
        if levels[severity] then return { nativeCode = 'infrastructure.postgres', nativeLevel = levels[severity], fatal = severity == 'FATAL' or severity == 'PANIC' } end
    elseif service == 'redis' then
        local severity = raw:match('^%d+:[A-Z]%s+%d%d%s+%a%a%a%s+%d%d%d%d%s+[%d:.]+%s+([*#%.%-])%s')
        if severity == '.' or severity == '-' then return { drop = true } end
        if severity then return { nativeCode = 'infrastructure.redis', nativeLevel = severity == '#' and 'Warning' or 'Info', fatal = false } end
    end
    return { rejectedReason = 'unparsed-native' }
end

-- 在 JSON parser 之前检查 Docker 元数据，载荷不能覆盖此检查。
local function preflight_impl(tag, timestamp, record)
    if not service_for(tag) then return { rejectedReason = 'unknown-source' } end
    if record.partial_message ~= nil then return { rejectedReason = 'fragment' } end
    if type(record.log) ~= 'string' then return { rejectedReason = 'invalid-event' } end
    local bytes = #record.log - (record.log:sub(-1) == '\n' and 1 or 0)
    if bytes > policy.maxEventBytes then return { rejectedReason = 'oversize' } end
    if service_for(tag) == 'postgres' or service_for(tag) == 'redis' then return native(tag, record.log) end
    -- 只把 log 交给 parser；Docker 原始字段不进入后续事件。
    return { log = record.log }
end

function preflight(tag, timestamp, record)
    local ok, output = pcall(preflight_impl, tag, timestamp, record)
    if not ok then output = { rejectedReason = 'invalid-event' } end
    if output.drop then return -1, timestamp, {} end
    return 2, timestamp, output
end

local function valid_time(value)
    if type(value) ~= 'string' or #value ~= 24 then return false end
    local y, m, d, h, minute, second = value:match('^(%d%d%d%d)%-(%d%d)%-(%d%d)T(%d%d):(%d%d):(%d%d)%.%d%d%dZ$')
    if not y then return false end
    y, m, d, h, minute, second = tonumber(y), tonumber(m), tonumber(d), tonumber(h), tonumber(minute), tonumber(second)
    if y < 1970 or m < 1 or m > 12 or h > 23 or minute > 59 or second > 59 then return false end
    local days = {31,28,31,30,31,30,31,31,30,31,30,31}
    if y % 4 == 0 and (y % 100 ~= 0 or y % 400 == 0) then days[2] = 29 end
    return d >= 1 and d <= days[m]
end

local function normalize_impl(tag, timestamp, record)
    -- preflight 的原生结果没有 log；业务 JSON 不能借 nativeCode 冒充原生来源。
    if (service_for(tag) == 'postgres' or service_for(tag) == 'redis') and record.nativeCode then
        local output = base(tag, timestamp, record.nativeCode, record.nativeLevel)
        output.sourceCategory, output.redacted, output.isFatal = 'infrastructure', true, record.fatal
        return output
    end
    if record.rejectedReason and record.log == nil then
        local reason = contains(policy.properties.rejectionReason.values, record.rejectedReason) and record.rejectedReason or 'invalid-event'
        return rejected(tag, timestamp, reason)
    end
    if record.schemaVersion ~= policy.schemaVersion then return rejected(tag, timestamp, 'invalid-schema') end
    if not uuid(record.eventId) or not policy.events[record.eventCode] or not contains({'Info','Warning','Error'}, record.level)
        or type(record.diagnostic) ~= 'boolean' or type(record.isFatal) ~= 'boolean'
        or not contains(policy.categories, record.sourceCategory) or record.mode ~= mode
        or record.service ~= service_for(tag) or record.deploymentId ~= deployment then
        return rejected(tag, timestamp, 'invalid-event')
    end
    if not valid_time(record.occurredAtUtc) or not valid_time(record.observedAtUtc) then return rejected(tag, timestamp, 'invalid-time') end
    if record.diagnostic and mode == 'Production' then return nil end
    if record.isFatal and record.level ~= 'Error' then return rejected(tag, timestamp, 'invalid-event') end
    local output = base(tag, timestamp, record.eventCode, record.level)
    output._producerEventId = record.eventId
    output.occurredAtUtc, output.observedAtUtc = record.occurredAtUtc, record.observedAtUtc
    output.diagnostic, output.isFatal, output.sourceCategory = record.diagnostic, record.isFatal, record.sourceCategory
    output.redacted = record.redacted == true or record.message ~= output.message or record.messageTemplate ~= output.messageTemplate
    output.truncated = record.truncated == true
    output.containerId = output.instanceId
    if record.instanceId == output.containerId:sub(1, 12) then output.instanceId = record.instanceId
    elseif record.instanceId ~= output.containerId then output.redacted = true end
    if record.release ~= release then output.redacted = true end
    if type(record.properties) == 'table' then
        for key, value in pairs(record.properties) do
            local rule = policy.properties[key]
            if rule and rule.type == 'number' and type(value) == 'number' and value >= rule.min and value <= rule.max
                and (not rule.integer or value == math.floor(value)) then output.properties[key] = value
            elseif rule and rule.type == 'enum' and contains(rule.values, value) then output.properties[key] = value
            else output.redacted = true end
        end
    else output.redacted = true end
    for _, key in ipairs({'traceId','spanId','operationId'}) do
        local value = record[key]
        local valid
        if key == 'operationId' then valid = uuid(value)
        else valid = type(value) == 'string' and #value == (key == 'traceId' and 32 or 16)
            and value:match('^[0-9a-f]+$') and value:find('[1-9a-f]') end
        if value ~= nil then if valid then output[key] = value else output.redacted = true end end
    end
    local allowed = { schemaVersion=true,eventId=true,eventCode=true,occurredAtUtc=true,observedAtUtc=true,deploymentId=true,service=true,
        instanceId=true,release=true,mode=true,sourceCategory=true,level=true,diagnostic=true,isFatal=true,message=true,messageTemplate=true,
        properties=true,traceId=true,spanId=true,operationId=true,normalizationStatus=true,redacted=true,truncated=true,log=true }
    for key, _ in pairs(record) do if not allowed[key] then output.redacted = true end end
    output.normalizationStatus = record.eventCode == 'runtime.unclassified' and 'unclassified' or 'normalized'
    return output
end

function normalize(tag, timestamp, record)
    local ok, output = pcall(normalize_impl, tag, timestamp, record)
    if not ok then output = rejected(tag, timestamp, 'invalid-event') end
    if output == nil then return -1, timestamp, {} end
    return 2, timestamp, output
end

-- UUID 插件只给规范化后的新对象加 ID，避免与原始对象同名键冲突。
function finalize(tag, timestamp, record)
    if record._producerEventId then record.eventId = record._producerEventId; record._producerEventId = nil end
    return 2, timestamp, record
end
