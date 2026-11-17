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
