# Quickstart: Hugo Matter CMS validation

**Feature**: `001-hugo-matter-cms`  
**Purpose**: Runnable validation guide for the connect → edit → save → preview → publish loop. Implementation details live in `tasks.md` / code; contracts and data model are linked, not duplicated.

## Prerequisites

- .NET 10 SDK
- Node.js (for Tailwind CSS build in `HugoMatter.Web`)
- Docker **or** Podman (for real Hugo site preview)
- GitHub account with a Hugo **Profile**-based repository you control (public or private)
- GitHub App credentials configured per [contracts/github-app-setup.md](./contracts/github-app-setup.md)

## One-time setup

```bash
# From repo root
cd src/HugoMatter.Web && npm install && npm run build:css && cd ../..

# Configure GitHub App secrets (see contracts/github-app-setup.md)
cd src/HugoMatter.ApiService
dotnet user-secrets set "GitHubApp:AppId" "<app-id>"
# ... ClientId, ClientSecret, PrivateKeyPem
cd ../..
```

## Start the app

```bash
aspire start
# or: dotnet run --project src/HugoMatter.AppHost
```

Open the Web external endpoint from the Aspire dashboard.

## Validation scenarios

### 1) Connect (P1)

1. In the UI, start **Connect repository**.
2. Complete GitHub App install/authorize for the Profile site repo.
3. **Expect**: Site shows as connected; default branch name displayed (need not be `main`).
4. Cancel/deny path: **Expect** site not connected.

Contracts: `POST /api/connection/authorize`, `GET /api/connection` — [api-openapi.yaml](./contracts/api-openapi.yaml).

### 2) Start session (P1)

1. From connected site with no session, **Start editing session**.
2. **Expect**: One open PR against the repo default branch; local session Active.
3. Attempt to start another session: **Expect** refusal / guided to existing session.

### 3) Create / edit / delete with Profile pack (P1)

1. Create a post; Profile frontmatter fields appear from the in-product pack (no schema files in the Hugo repo).
2. Edit body + frontmatter; delete an existing item in the buffer.
3. **Expect**: Fields/defaults match Theme Pack contract ([theme-pack-schema.json](./contracts/theme-pack-schema.json)); unknown frontmatter preserved on round-trip when saving existing files.

### 4) Save (P1)

1. Save the session.
2. **Expect**: New commit(s) on the session branch; PR reflects changes; `hasUnsavedLocalEdits` false.
3. Force auth failure (optional): **Expect** clear error, no false success.

### 5) Previews (P2)

1. **In-editor preview**: **Expect** approximate HTML; shortcodes may not render; can show unsaved buffer.
2. With unsaved edits, start **site preview**: **Expect** preview uses only last saved session tip (unsaved omitted).
3. Save, then refresh/restart site preview: **Expect** saved edits visible in real Hugo output.
4. **Expect**: Preview runs in Docker/Podman isolation (workspace-bound).

### 6) Publish / discard (P2)

**Publish happy path**

1. Session with saved changes, clean buffer → Publish.
2. **Expect**: PR merged into default branch; session branch deleted; session ended.

**Publish guards**

- Unsaved edits → blocked with explanation.
- No changes vs default → blocked with explanation.
- Branch protection / conflict → failed with explanation; session + PR remain.

**Discard**

- Clean buffer → PR closed, branch deleted, session ended.
- Dirty buffer → warning + confirmation required before discard.

### 7) Automated checks (once implemented)

```bash
dotnet test tests/HugoMatter.Core.Tests
dotnet test tests/HugoMatter.ApiService.Tests
# Thin Playwright smoke when available:
dotnet test tests/HugoMatter.E2E.Tests
```

**Priority coverage**: session/PR lifecycle, theme-pack application, preview orchestration boundaries. Do **not** require AppHost modelling tests.

## Success signal

Owner can complete connect → session → edit Profile post → save → site preview → publish without hand-editing the repo, within the success criteria in [spec.md](./spec.md) (SC-001, SC-002, SC-007).
