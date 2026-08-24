# Site preview (local development)

Hugo Matters runs real Hugo site previews in local Docker/Podman containers during development. This document covers behaviour that is intentional for local dev but would need revisiting before any hosted/multi-tenant deployment.

## WSL preview URLs and LAN reachability

On WSL2, the preview orchestrator may publish the preview URL using the WSL network interface address (for example `http://172.x.x.x:13130`) instead of `127.0.0.1`. That is deliberate: a browser on Windows often cannot reach Hugo when the container port is published only on the WSL loopback.

**Implication:** the Hugo preview server may be reachable from other machines on the same local network, not only from your browser. Hugo binds inside the container on `0.0.0.0`, and the published host port is exposed on the WSL interface.

**This is acceptable for local development** on a trusted network. Do not rely on this URL scheme in production; a hosted preview would need an authenticated reverse proxy or tunnel instead of a raw LAN address.

## Related implementation

| Area | Location |
|------|----------|
| Browser host resolution (WSL) | `DockerSitePreviewOrchestrator.ResolveBrowserPreviewHost()` |
| Readiness timeout (90s) | `StartingPreviewReconciler.ReadinessTimeoutSeconds` |
| UI startup poll window (100s) | `SitePreview.razor` `PollUntilPreviewSettledAsync` |
