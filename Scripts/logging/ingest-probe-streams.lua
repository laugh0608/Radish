-- Docker fluentd attaches source outside the application's log string.
-- This routing is scoped to the isolated ingest probe.
function route_probe_stream(tag, timestamp, record)
    local service, container = tag:match('^radish%.([%w-]+)%.([0-9a-f]+)$')
    if not container or #container ~= 64 then return 0, timestamp, record end
    if (service == 'dbmigrate' and record.source == 'stdout')
        or (service == 'api' and record.source == 'stderr') then
        return -1, timestamp, {}
    end
    return 0, timestamp, record
end

-- Only the optional final probe filter emits ordinals, before Chunk I/O.
local growth_series = os.getenv('RADISH_PROBE_GROWTH_SERIES')
function trace_growth_append(tag, timestamp, record)
    local id = record.eventId
    if growth_series and type(id) == 'string' and #id == 36 and id:sub(1, 24) == growth_series then
        local sequence = tonumber(id:sub(25), 16)
        if sequence then io.stderr:write('probe-growth-append=', tostring(sequence), '\n') end
    end
    return 0, timestamp, record
end
