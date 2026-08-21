# Research: Hugo Matter CMS

**Feature**: `001-hugo-matter-cms`  
**Date**: 2026-08-21

All Technical Context unknowns from the plan are resolved below.

---

## 1. Solution shape and project boundaries

**Decision**: Extend the existing Aspire starter (`HugoMatter.Web`, `HugoMatter.ApiService`, `ServiceDefaults`, `AppHost`) with three libraries: `HugoMatter.Core` (domain + ports), `HugoMatter.Infrastructure` (GitHub App, local metadata, container preview), `HugoMatter.ThemePacks` (Profile pack + registry).

**Rationale**: Matches constitution VIII (clear boundaries, YAGNI) and user preference for straightforward Aspire patterns. Keeps domain/theme packs reusable for a future hosted SaaS without dragging Blazor or Docker into Core.

**Alternatives considered**:
- Put all logic in ApiService — rejected (harder to unit-test and reuse; couples domain to ASP.NET hosting).
- Separate Preview microservice in AppHost — rejected (extra service for on-demand work; overkill for v1).
- Monolithic Web-only app calling GitHub directly — rejected (weaker separation; harder API contracts and E2E seams).

---

## 2. GitHub App as primary repository access

**Decision**: Use a GitHub App from day one for private and public repos. Solo owner installs the App on their site repository. Credentials (App ID, Client ID, Client secret, private key PEM) live in user secrets / environment variables — never in source or generated site content. Prefer installation access tokens (short-lived) over PATs as the primary model.

**Minimum permissions (least privilege)**:
- Repository Contents: Read & write (read trees, create commits)
- Pull requests: Read & write (open, update, merge, close)
- Metadata: Read (required)
- Optionally: Administration not required; avoid broad org permissions

**Dev setup (explicit plan item)**:
1. Create a GitHub App in the owner’s account (or org) with the permissions above.
2. Set callback URL to the local ApiService/Web OAuth callback (Aspire-forwarded HTTPS/HTTP URL).
3. Generate a private key; store path or PEM via `dotnet user-secrets` on ApiService (and document env var names).
4. Document App ID / Client ID / Client secret / webhook optional (webhooks not required for v1 if we poll/refresh on user actions).
5. Owner completes install → select the Hugo site repo → product records `installationId` + repo identity locally.

**Rationale**: Constitution VII prefers durable least-privilege platform integrations over ad-hoc long-lived PATs. Spec FR-001/FR-002 require secure owner-controlled authorization for private and public repos.

**Alternatives considered**:
- PAT-primary — rejected (not least-privilege durable model; user explicitly forbade as primary).
- Device flow only without App installation — rejected (weaker repo scoping for private repos).
- Fine-grained PAT as fallback helper for emergencies — may be documented later as escape hatch; not v1 primary path.

---

## 3. Editing session = branch + pull request

**Decision**: Starting a session creates a branch from the repository’s **configured default branch** (FR-027) and opens exactly one PR against that default. Save creates commit(s) on the session branch. Publish merges the PR (merge method: merge commit or squash — prefer **squash** for cleaner history unless Profile/site docs suggest otherwise; default to GitHub’s allowed merge method, preferring squash when available). On successful merge, delete session branch and clear local session. Discard closes PR without merge, deletes branch, clears local session. Enforce one active session per connected site via local metadata + GitHub state (open PR with session label/prefix).

**Branch naming**: `hugo-matter/session-{shortId}` (predictable prefix for identification and cleanup).

**Publish guards** (spec clarifications):
- Block if unsaved local buffer dirty
- Block if no file changes vs default branch
- On protection/conflict/auth failure: fail clearly; leave PR + session intact

**Rationale**: Directly implements constitution II and FR-004–FR-005, FR-014–FR-017, FR-023–FR-027.

**Alternatives considered**:
- Commit directly to default branch — rejected (no review artifact; violates session model).
- Multiple PRs / stacked sessions — rejected (v1 single-session rule).
- In-product conflict editor — rejected (explicit non-goal; fail and keep session).

---

## 4. Theme packs in-product (Hugo Profile first)

**Decision**: Theme packs are versioned modules registered in `HugoMatter.ThemePacks`. Each pack supplies: pack id/version, supported content types (post/page), frontmatter field definitions (type, label, required, default), editor field mapping, optional minimal site-config field set, and a compatibility probe (e.g., detect Profile theme markers in `hugo.toml` / theme folder). Owner repos do **not** ship CMS schema files.

**Pack contract**: JSON Schema documented in `contracts/theme-pack-schema.json`; Profile pack ships as embedded resources + C# binder for strong typing where useful.

**Non-Profile sites**: Clear unsupported messaging rather than silently applying Profile fields.

**Rationale**: Constitution III; FR-008–FR-010; US3/US8/US9.

**Alternatives considered**:
- Schema files in owner repo — rejected (constitution + user input).
- Hard-code Profile in UI pages — rejected (blocks second pack without redesign).
- Full theme introspection from Hugo modules at runtime — deferred (useful later; not required for Profile day-one).

---

## 5. Frontmatter and content round-trip

**Decision**: Parse Hugo content files as YAML frontmatter + body. Preserve unknown keys and ordering as far as practical (parse to ordered dictionary / YAML DOM). On save, rewrite only edited fields + body; do not drop unknown frontmatter. Deletes remove the content file(s) via Git commit on the session branch.

**Library**: YamlDotNet for YAML; custom thin Hugo content document type in Core.

**Rationale**: FR-020 / constitution VII (fidelity over flashy editors).

**Alternatives considered**:
- Strongly typed Profile-only model that drops unknowns — rejected (corruption risk).
- TOML-only frontmatter — Hugo Profile commonly uses YAML; stick to YAML for v1 Profile pack (document if a site uses TOML frontmatter as unsupported or best-effort later).

---

## 6. Tailwind CSS + Lucide on Blazor Server

**Decision**: Integrate Tailwind via npm + Tailwind CLI (or standalone CLI) in `HugoMatter.Web`:
- Source: `Components/app.css` or `Styles/input.css` with `@tailwind` directives
- Build output: `wwwroot/css/app.css` (or replace starter CSS)
- MSBuild / npm script: `npm run build:css` on build; `npm run watch:css` for dev
- Prefer Tailwind v4 if tooling is stable for this workflow; otherwise Tailwind v3 + PostCSS — pick one in implementation and lock versions in `package.json`
- Lucide: use Lucide static SVGs wrapped in a small `Icon` Blazor component (or Lucide’s icon package if JS-free SVG set is available); avoid pulling a full icon+component design system
- Shared primitives: `Button`, `TextField`, `Select`, `Dialog`, `Banner`, `Icon` — no MudBlazor / Fluent UI

**Explicit plan item**: Wire Tailwind build into the Web project’s local and CI build so CSS is produced before/with `dotnet build`. Document Node.js prerequisite in quickstart.

**Rationale**: User stack preference; keeps UI lightweight and branded without a heavy component framework.

**Alternatives considered**:
- MudBlazor / Fluent UI — rejected by user unless later adopted.
- CDN Tailwind Play CDN in production — rejected (not for durable app CSS; poor for CSP and versioning).
- Pure scoped CSS without utility framework — slower for v1 UI velocity.

---

## 7. In-editor preview vs site preview

**Decision**:
- **In-editor preview**: Client/server-side Markdown → HTML via Markdig (or similar); shortcodes left as literal/placeholder; may reflect live unsaved buffer.
- **Site preview**: Infrastructure clones/checks out **saved session branch** into a workspace directory, runs Hugo in Docker/Podman with bind-mount of that workspace, publishes a localhost URL to the UI. Unsaved buffer excluded (FR-013). Preview lifecycle is owned by ApiService/Infrastructure and is independent of the Blazor process lifetime (stop/cleanup on discard, publish, or explicit stop).

**Container runtime**: Prefer Docker; accept Podman if Docker CLI-compatible. Image: official `hugo` / `klakegg/hugo` / `ghcr.io/gohugoio/hugo` — pin a version known to work with Profile. Network: publish only a host port; no host filesystem access beyond the workspace mount.

**Abstraction**: `ISitePreviewOrchestrator` in Core so a future hosted worker could replace local containers without rewriting session/content flows.

**Rationale**: Constitution IV/VII; FR-011/FR-013; clarifications on unsaved vs saved.

**Alternatives considered**:
- Hugo as AppHost permanent resource — rejected (idle cost; session-scoped lifecycle fits better).
- In-process Hugo binary — weaker isolation vs constitution preview isolation.
- Approximate whole-site mock for “site preview” — rejected by spec (must be real Hugo).

---

## 8. Local metadata persistence

**Decision**: Persist only: GitHub App installation binding, connected repo identity, active session (branch, PR number, dirty flag optional), last preview state. Prefer **SQLite** via EF Core for simple queries and one-active-session enforcement; store DB under a user-local application data path. Content bodies are never the source of truth in SQLite.

**Rationale**: FR-006 / constitution II; enough structure for “one session” uniqueness without inventing a CMS database.

**Alternatives considered**:
- JSON file only — acceptable for true single-user; SQLite preferred for clearer constraints and testability.
- Full content sync DB — rejected (parallel store).

---

## 9. Spell checking (multi-language)

**Decision**: Use browser-native spellcheck on editor text surfaces (`spellcheck="true"`) with appropriate `lang` on the editing root (per-content or user-selected language). Support switching among at least two languages (e.g., `en` and one additional language the owner selects). No Chart.js. Avoid heavy server-side spell engines in v1 unless native spellcheck proves insufficient on target browsers.

**Rationale**: Meets FR-012 / SC-005 with minimal dependencies; aligns with YAGNI.

**Alternatives considered**:
- Hunspell / WeCantSpell server-side — more control, more packaging cost; defer.
- Single fixed language — fails multi-language requirement.

---

## 10. Testing strategy

**Decision**:
- **Unit (mandatory)**: xUnit v3 + NSubstitute + built-in Assert for Core (session state machine, publish/discard guards, theme-pack field application, frontmatter round-trip, path validation).
- **API tests**: Handler/service tests with substituted GitHub and preview ports.
- **E2E**: Playwright project for one primary smoke journey when auth can be stubbed or a test double GitHub is available; focus on workflow, not pixel chrome.
- **Do not** test AppHost modelling.
- **Prioritize**: GitHub session/PR lifecycle, theme-pack schema application, preview orchestration boundaries.

**Rationale**: Matches user testing standards and constitution Quality Expectations.

**Alternatives considered**:
- Moq / FluentAssertions — explicitly disallowed.
- Broad UI Playwright suite — brittle; prefer thin high-value path.

---

## 11. API style between Web and ApiService

**Decision**: Keep Aspire starter pattern: Web calls ApiService over HTTP with service discovery (`https+http://apiservice`). Expose versioned REST JSON endpoints documented in `contracts/api-openapi.yaml`. Blazor components use typed HttpClient(s). OpenAPI already referenced on ApiService — extend it for real endpoints; remove Weather sample during implementation.

**Rationale**: Straightforward Aspire pattern; clear contract for future non-Blazor clients / hosted SaaS.

**Alternatives considered**:
- Blazor calling GitHub directly — rejected (secrets and domain logic in UI).
- gRPC — unnecessary complexity for v1 local tool.

---

## 12. Chart.js

**Decision**: Not used in this feature.

**Rationale**: No charts required for editing-session CMS.

---

## Resolved Technical Context checklist

| Item | Resolution |
|------|------------|
| Language/Version | .NET 10 |
| UI | Blazor Server + Tailwind + Lucide + small primitives |
| Orchestration | Existing Aspire AppHost (Web + ApiService only) |
| Auth to repos | GitHub App (dev credentials via user-secrets) |
| Content SoT | GitHub Git |
| Local store | SQLite metadata only |
| Preview | Docker/Podman Hugo containers behind port interface |
| Theme | In-product Profile pack |
| Unit tests | xUnit v3 + NSubstitute + Assert |
| E2E | Playwright (thin) |
| AppHost tests | None |
