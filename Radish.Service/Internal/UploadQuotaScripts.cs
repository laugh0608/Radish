namespace Radish.Service.Internal;

/// <summary>
/// 上传配额的原子 Redis 操作。容量始终为十进制字符串，避免 Lua double 的精度与格式化损失。
/// 所有可预判的状态 / 溢出错误均先检查再写入；不提供基础设施故障下的事务回滚。
/// </summary>
internal static class UploadQuotaScripts
{
    private const string StateFunctions = """
        local int64Max = '9223372036854775807'
        local function invalid()
            error('RADISH_UPLOAD_QUOTA_INVALID_STATE invalid or overflowing quota state', 0)
        end
        local function compare(a, b)
            if #a ~= #b then return #a < #b and -1 or 1 end
            if a == b then return 0 end
            return a < b and -1 or 1
        end
        local function integer(value, maximum)
            if type(value) ~= 'string' or not string.match(value, '^%d+$')
                or (#value > 1 and string.sub(value, 1, 1) == '0')
                or compare(value, maximum or int64Max) > 0 then invalid() end
            return value
        end
        local function add(a, b)
            local result, carry, i, j = {}, 0, #a, #b
            while i > 0 or j > 0 or carry > 0 do
                local digit = carry
                if i > 0 then digit = digit + string.byte(a, i) - 48; i = i - 1 end
                if j > 0 then digit = digit + string.byte(b, j) - 48; j = j - 1 end
                table.insert(result, 1, string.char(48 + digit % 10))
                carry = math.floor(digit / 10)
            end
            return integer(table.concat(result))
        end
        -- 调用方已确认 a >= b；各位运算不把完整容量转为 number。
        local function subtract(a, b)
            local result, borrow, j = {}, 0, #b
            for i = #a, 1, -1 do
                local digit = string.byte(a, i) - 48 - borrow
                if j > 0 then digit = digit - string.byte(b, j) + 48; j = j - 1 end
                borrow = digit < 0 and 1 or 0
                table.insert(result, 1, string.char(48 + (digit + 10) % 10))
            end
            local value = string.gsub(table.concat(result), '^0+', '')
            return value == '' and '0' or value
        end
        local function parseReservation(value, reservationKey)
            local size, rateKey, dailyKey, expires = string.match(value, '^([^|]+)|([^|]+)|([^|]+)|([^|]+)$')
            if not size then invalid() end
            integer(size)
            if size == '0' then invalid() end
            integer(expires, '253402300799')
            local prefix = string.sub(reservationKey, 1, #reservationKey - #'reservations')
            local ratePrefix, dailyPrefix = prefix .. 'rate:', prefix .. 'daily:'
            if string.sub(rateKey, 1, #ratePrefix) ~= ratePrefix
                or not string.match(string.sub(rateKey, #ratePrefix + 1), '^%d%d%d%d%d%d%d%d%d%d%d%d$')
                or string.sub(dailyKey, 1, #dailyPrefix) ~= dailyPrefix
                or not string.match(string.sub(dailyKey, #dailyPrefix + 1), '^%d%d%d%d%d%d%d%d$') then invalid() end
            return size, dailyKey, tonumber(expires)
        end
        local function readCounter(key, maximum)
            local value = redis.call('GET', key)
            return value and integer(value, maximum) or '0'
        end
        local function readReservations(key, dailyKey, now)
            local entries, expired, live = redis.call('HGETALL', key), {}, {}
            local count, pending = 0, '0'
            for i = 1, #entries, 2 do
                local size, entryDailyKey, expires = parseReservation(entries[i + 1], key)
                if expires <= now then
                    table.insert(expired, entries[i])
                else
                    live[entries[i]] = true
                    count = count + 1
                    if entryDailyKey == dailyKey then pending = add(pending, size) end
                end
            end
            return count, pending, expired, live
        end
        local function prune(key, expired)
            for _, id in ipairs(expired) do redis.call('HDEL', key, id) end
        end
        local function removeTarget(key, id)
            redis.call('HDEL', key, id)
            if redis.call('HLEN', key) == 0 then redis.call('DEL', key) end
        end
        """;

    internal const string Acquire = StateFunctions + """

        local now, uploadId = tonumber(ARGV[1]), ARGV[2]
        local fileSize, maxDailySize = integer(ARGV[3]), integer(ARGV[6])
        local maxConcurrent, maxPerMinute = tonumber(ARGV[4]), tonumber(ARGV[5])
        local expiresAt, reservationTtl = integer(ARGV[7], '253402300799'), tonumber(ARGV[8])
        local count, pending, expired, live = readReservations(KEYS[1], KEYS[3], now)
        local attempts = tonumber(readCounter(KEYS[2], '2147483647'))
        local completed = readCounter(KEYS[3])
        local occupied = add(completed, pending)
        local result
        if live[uploadId] then result = {1, '0', '0'}
        elseif count >= maxConcurrent then result = {2, tostring(count), ARGV[4]}
        elseif attempts >= maxPerMinute then result = {3, tostring(attempts), ARGV[5]}
        elseif compare(occupied, maxDailySize) > 0 or compare(fileSize, subtract(maxDailySize, occupied)) > 0 then
            result = {4, occupied, maxDailySize}
        end
        -- 上面的所有状态和算术检查完成后，才允许清理 / 消耗分钟次数。
        prune(KEYS[1], expired)
        if result then return result end
        redis.call('HSET', KEYS[1], uploadId, fileSize .. '|' .. KEYS[2] .. '|' .. KEYS[3] .. '|' .. expiresAt)
        redis.call('INCR', KEYS[2])
        redis.call('EXPIRE', KEYS[2], 120)
        if redis.call('TTL', KEYS[1]) < reservationTtl then redis.call('EXPIRE', KEYS[1], reservationTtl) end
        return {1, '0', '0'}
        """;

    internal const string Complete = StateFunctions + """

        local value = redis.call('HGET', KEYS[1], ARGV[2])
        if not value then return {0, '0'} end
        local size, dailyKey, expires = parseReservation(value, KEYS[1])
        if dailyKey ~= KEYS[2] then invalid() end
        if expires <= tonumber(ARGV[1]) then
            removeTarget(KEYS[1], ARGV[2])
            return {0, '0'}
        end
        local completed = readCounter(KEYS[2])
        add(completed, size)
        -- INCRBY 接收原始十进制字符串；忽略返回到 Lua 后可能舍入的 number。
        redis.call('INCRBY', KEYS[2], size)
        redis.call('EXPIRE', KEYS[2], tonumber(ARGV[3]))
        removeTarget(KEYS[1], ARGV[2])
        return {1, size}
        """;

    internal const string Fail = """
        local removed = redis.call('HDEL', KEYS[1], ARGV[1])
        if redis.call('HLEN', KEYS[1]) == 0 then redis.call('DEL', KEYS[1]) end
        return removed
        """;

    internal const string Statistics = StateFunctions + """

        local count, pending, expired = readReservations(KEYS[1], KEYS[3], tonumber(ARGV[1]))
        local attempts = readCounter(KEYS[2], '2147483647')
        local completed = readCounter(KEYS[3])
        add(completed, pending)
        prune(KEYS[1], expired)
        return {count, attempts, completed, pending}
        """;
}
