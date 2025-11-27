(function () {
    const originLabel = document.getElementById("current-origin");
    originLabel.textContent = window.location.origin;

    const statusEl = document.getElementById("health-status");
    const downstreamEl = document.getElementById("downstream-status");

    // 检查 Gateway 自身健康状态
    (async () => {
        const candidates = ["/healthz", "/health", "/api/health"];
        for (const endpoint of candidates) {
            try {
                const start = performance.now();
                const response = await fetch(endpoint, { cache: "no-store" });
                if (response.ok) {
                    const duration = Math.round(performance.now() - start);
                    statusEl.textContent = `Gateway 可用 · ${endpoint} · ${duration} ms`;
                    statusEl.dataset.state = "ok";
                    return;
                }
            } catch (error) {
                // 忽略并尝试下一个端点
            }
        }
        statusEl.textContent = "Gateway 健康检查端点未响应";
        statusEl.dataset.state = "fail";
    })();

    // 检查下游 API 服务状态
    (async () => {
        try {
            const start = performance.now();
            const response = await fetch("https://localhost:5101/health", {
                cache: "no-store",
                mode: "cors"
            });
            if (response.ok) {
                const duration = Math.round(performance.now() - start);
                downstreamEl.textContent = `API 服务可用 · ${duration} ms`;
                downstreamEl.dataset.state = "ok";
            } else {
                downstreamEl.textContent = `API 服务响应异常 (${response.status})`;
                downstreamEl.dataset.state = "fail";
            }
        } catch (error) {
            downstreamEl.textContent = "API 服务未启动或无法访问";
            downstreamEl.dataset.state = "fail";
        }
    })();
})();
