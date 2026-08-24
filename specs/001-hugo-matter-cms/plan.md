# Implementation Plan: Hugo Matters CMS

**Branch**: `001-hugo-matter-cms` | **Date**: 2026-08-21 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-hugo-matter-cms/spec.md`

## Summary

Hugo Matters is a local-first CMS for a solo owner to connect one GitHub-hosted Hugo site, edit posts/pages through a theme-pack–aware UI (Hugo Profile first), save as commits on a session branch/PR, preview with real Hugo in an isolated container, and publish by merging into the repository’s default branch (or discard by closing without merge).

Technical approach: extend the existing .NET 10 Aspire solution (Blazor Server Web + ApiService + ServiceDefaults + AppHost). Domain/session/theme-pack logic lives in reusable class libraries; GitHub App is the repository access model; **Web owns GitHub user sign-in** (`HostedGitHubAuthGateway` pattern, `/auth/callback`); ApiService binds the site via `ConnectAsync` using installation tokens only. Only connection/session metadata is persisted locally; Git remains source of truth. UI uses Tailwind CSS + Lucide Icons with small shared Blazor primitives (no MudBlazor/Fluent). Tests: xUnit v3 + NSubstitute + built-in Assert; Playwright for a thin E2E smoke path. AppHost modelling is not tested.

## Technical Context

**Language/Version**: .NET 10 (`net10.0`)

**Primary Dependencies**:
- ASP.NET Core Blazor Server (interactive server components)
- .NET Aspire (local orchestration of Web + ApiService)
- Octokit.NET (or thin GitHub REST client) for GitHub App installation + Contents/Git/PR APIs
- YamlDotNet (or equivalent) for Hugo frontmatter round-trip
- Tailwind CSS (CLI build into Web `wwwroot`)
- Lucide Icons (static SVG / icon component set)
- Docker or Podman CLI for isolated Hugo site-preview containers
- Markdig (or similar) for approximate in-editor Markdown preview (shortcodes omitted)

**Storage**:
- Local app metadata only (SQLite via EF Core or a small JSON/file store under a user-local data directory): connected site, GitHub installation binding, active session references (branch name, PR number), preview workspace paths
- GitHub repository = system of record for all content and session commits
- No parallel content database for posts/pages

**Testing**:
- Unit: xUnit v3, NSubstitute, built-in `Assert` only
- E2E: Playwright for key journeys (connect → edit → save → preview → publish)
- Do **not** test Aspire AppHost modelling/orchestration
- Do **not** introduce FluentAssertions, AwesomeAssertions, Shouldly, Moq, NUnit, or MSTest

**Target Platform**: Local machine (WSL2/Linux/macOS/Windows with Docker or Podman available); browser UI against Blazor Server

**Project Type**: Local web application (Aspire-orchestrated Blazor Server + API)

**Performance Goals**:
- Connect → ready-to-edit under ~10 minutes first use (SC-001; dominated by GitHub App setup)
- Simple post create/edit/save under ~15 minutes (SC-002)
- Interactive editor responsiveness suitable for solo editing (no multi-tenant SLAs)
- Site preview start within a few minutes for a typical Profile site (best-effort; not a hard SLA)

**Constraints**:
- One connected site, one active editing session (branch + open PR) in v1
- GitHub App auth from day one (PATs not primary)
- Site preview isolated from Blazor process; only saved session content
- Theme schema lives in product packs, not owner repos
- Secrets never committed, logged, or embedded in generated site content
- v1 local-only; domain/API/theme-pack boundaries must not assume containerized Hugo forever
- Non-goals: multi-session concurrency, full media/menu CMS, shortcode WYSIWYG, production multi-tenant hosting

**Scale/Scope**:
- Solo owner, single site
- Day-one theme pack: Hugo Profile (frontmatter schema, defaults, editor field mapping, minimal config fields)
- Content: posts/pages create/edit/delete + minimal Profile site config
- In-editor preview approximate; real Hugo for full-site preview

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Gate | Status |
|-----------|------|--------|
| **I. Security by Default** | Least-privilege GitHub App permissions; secrets via user-secrets/env only; path/content validation; preview confined to workspace; safe failure on auth/merge errors | PASS — see research (GitHub App scopes, preview isolation) |
| **II. Git Is Source of Truth** | Session = branch + PR; save = commit(s); publish = merge to default branch; discard = close + delete branch; no parallel content store | PASS |
| **III. Theme Knowledge in Product** | Versioned Hugo Profile pack in-repo; isolatable pack interface; owner repos stay ordinary Hugo trees | PASS |
| **IV. Local-First Operation** | Aspire local orchestration; Hugo preview via Docker/Podman; domain/API not coupled to containers as the only future preview model | PASS — preview behind an abstraction |
| **V. Content-First Scope** | Posts/pages + minimal Profile config; no menus/media/data-file CMS in v1 | PASS |
| **VI. Simple Session Model** | Enforce single active session per connected site | PASS |
| **VII. Faithful Preview & Durable Integrations** | Approximate in-editor preview; real Hugo site preview; GitHub App over PAT-primary; round-trip fidelity for frontmatter/body | PASS |
| **VIII. Simplicity & Clear Boundaries** | Extend Aspire starter; add Core + ThemePacks libraries; avoid extra services; stack choices in plan not constitution | PASS |
| **Quality Expectations** | Automated coverage for session lifecycle, theme-pack application, GitHub ops (mocked), preview isolation boundaries; thin Playwright smoke | PASS |

**Post–Phase 1 re-check**: Design artifacts (`data-model.md`, `contracts/`, `quickstart.md`) preserve the same gates. No unjustified violations. Complexity Tracking remains empty.

## Project Structure

### Documentation (this feature)

```text
specs/001-hugo-matter-cms/
├── plan.md              # This file
├── research.md          # Phase 0
├── data-model.md        # Phase 1
├── quickstart.md        # Phase 1
├── contracts/           # Phase 1
│   ├── api-openapi.yaml
│   ├── theme-pack-schema.json
│   └── github-app-setup.md
└── tasks.md             # Phase 2 (/speckit-tasks — not created here)
```

### Source Code (repository root)

```text
HugoMatters.slnx
aspire.config.json
src/
├── HugoMatters.AppHost/           # Aspire orchestration (Web + ApiService); not unit-tested
├── HugoMatters.ServiceDefaults/   # Shared Aspire defaults / health / OTel
├── HugoMatters.Web/               # Blazor Server UI (Tailwind, Lucide, editor, preview panes)
│   ├── Components/
│   ├── wwwroot/                  # Built CSS, icons, static assets
│   ├── package.json              # Tailwind CLI / Lucide tooling (as needed)
│   └── ...
├── HugoMatters.ApiService/        # HTTP API: connect, session, content, preview, publish
├── HugoMatters.Core/              # Domain: session lifecycle, content model, frontmatter I/O,
│                                 # theme-pack interfaces, preview port, GitHub port
├── HugoMatters.Infrastructure/    # GitHub App client, local metadata store, Docker/Podman preview
└── HugoMatters.ThemePacks/        # Pack registry + Hugo Profile pack (schema, defaults, mapping)

tests/
├── HugoMatters.Core.Tests/        # xUnit v3 unit tests (session, frontmatter, theme packs)
├── HugoMatters.ApiService.Tests/  # API/handler tests with NSubstitute doubles
└── HugoMatters.E2E.Tests/         # Playwright high-value journeys (optional early; required for smoke)
```

**Structure Decision**: Keep the existing Aspire four-project shape and add **Core**, **Infrastructure**, and **ThemePacks** class libraries plus test projects. ApiService hosts application endpoints and orchestrates GitHub + preview via Core ports; Web remains a Blazor Server client of the API (Aspire service discovery). No additional long-running Aspire resources beyond Web + ApiService for v1 (Hugo runs as on-demand containers managed by Infrastructure, not as a permanent AppHost service).

## Complexity Tracking

> No constitution violations requiring justification.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
