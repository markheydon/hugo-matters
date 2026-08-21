---
description: "Task list for Hugo Matter CMS feature implementation"
---

# Tasks: Hugo Matter CMS

**Input**: Design documents from `/specs/001-hugo-matter-cms/`

**Prerequisites**: plan.md (required), spec.md (required for user stories), research.md, data-model.md, contracts/

**Tests**: Unit and API tests are included per plan.md, research.md, and constitution Quality Expectations. E2E smoke test is in the Polish phase.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Path Conventions

- **Source**: `src/` at repository root (Aspire Web + ApiService + Core + Infrastructure + ThemePacks)
- **Tests**: `tests/` at repository root (Core, ApiService, E2E)

---

## Phase 0: Repo Bootstrap

**Purpose**: Repository hygiene and MSBuild baseline before feature implementation

- [x] T000 Add `.gitignore` (VisualStudio baseline + project extras) and untrack committed `bin/` / `obj/` artifacts
- [x] T000a [P] Add `.editorconfig`, `Directory.Build.props`, `global.json`, and `nuget.config` at repository root
- [x] T000b [P] Add `Directory.Packages.props` (Central Package Management), migrate starter template package references, and add MIT `LICENSE` + root `README.md`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [ ] T001 Add HugoMatter.Core, HugoMatter.Infrastructure, and HugoMatter.ThemePacks class library projects to HugoMatter.slnx per plan.md
- [ ] T002 [P] Create HugoMatter.Core.Tests xUnit v3 project in tests/HugoMatter.Core.Tests/HugoMatter.Core.Tests.csproj
- [ ] T003 [P] Create HugoMatter.ApiService.Tests xUnit v3 project in tests/HugoMatter.ApiService.Tests/HugoMatter.ApiService.Tests.csproj
- [ ] T004 [P] Create HugoMatter.E2E.Tests Playwright project in tests/HugoMatter.E2E.Tests/HugoMatter.E2E.Tests.csproj
- [ ] T005 Add versionless NuGet package references (YamlDotNet, Octokit, EF Core SQLite, Markdig) to src/HugoMatter.Core/ and src/HugoMatter.Infrastructure/ per research.md (versions in Directory.Packages.props)
- [ ] T006 Wire project references: Core ← Infrastructure ← ApiService; ThemePacks → Core; Web typed HttpClient → ApiService only
- [ ] T007 [P] Configure Tailwind CSS build pipeline in src/HugoMatter.Web/package.json and src/HugoMatter.Web/Styles/input.css
- [ ] T008 [P] Add Lucide Icon Blazor component in src/HugoMatter.Web/Components/Shared/Icon.razor
- [ ] T009 [P] Create shared UI primitives (Button, TextField, Select, Dialog, Banner) in src/HugoMatter.Web/Components/Shared/
- [ ] T010 Align GitHub App dev credential setup with specs/001-hugo-matter-cms/contracts/github-app-setup.md in src/HugoMatter.ApiService/Program.cs configuration

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [ ] T011 Define domain entities ConnectedSite, EditingSession, and ContentItem in src/HugoMatter.Core/Models/
- [ ] T012 [P] Define port interfaces IGitHubRepository, ISitePreviewOrchestrator, IThemePackRegistry, and IMetadataStore in src/HugoMatter.Core/Ports/
- [ ] T013 Implement repo-relative path validation in src/HugoMatter.Core/Security/ContentPathValidator.cs
- [ ] T014 Implement Hugo content document and YAML frontmatter round-trip in src/HugoMatter.Core/Content/HugoContentDocument.cs using YamlDotNet
- [ ] T015 Implement session state machine and publish/discard guard rules in src/HugoMatter.Core/Sessions/SessionLifecycle.cs
- [ ] T016 Implement SQLite EF Core metadata context in src/HugoMatter.Infrastructure/Persistence/HugoMatterDbContext.cs
- [ ] T017 [P] Implement ConnectedSite and EditingSession repositories in src/HugoMatter.Infrastructure/Persistence/
- [ ] T018 Implement GitHub App configuration options in src/HugoMatter.Infrastructure/GitHub/GitHubAppOptions.cs
- [ ] T019 Implement GitHub App client (installation tokens, Contents, Git, Pull Request APIs) in src/HugoMatter.Infrastructure/GitHub/GitHubAppClient.cs
- [ ] T020 Implement theme pack schema models matching specs/001-hugo-matter-cms/contracts/theme-pack-schema.json in src/HugoMatter.ThemePacks/Models/
- [ ] T021 Implement ThemePackRegistry in src/HugoMatter.ThemePacks/ThemePackRegistry.cs
- [ ] T022 Register Core and Infrastructure services in src/HugoMatter.ApiService/Program.cs
- [ ] T023 Add typed HttpClient with Aspire service discovery in src/HugoMatter.Web/Program.cs
- [ ] T024 Remove WeatherForecast sample code from src/HugoMatter.ApiService/ and src/HugoMatter.Web/
- [ ] T025 [P] Add unit tests for SessionLifecycle guards in tests/HugoMatter.Core.Tests/Sessions/SessionLifecycleTests.cs
- [ ] T026 [P] Add unit tests for frontmatter round-trip fidelity in tests/HugoMatter.Core.Tests/Content/HugoContentDocumentTests.cs
- [ ] T027 [P] Add unit tests for ContentPathValidator in tests/HugoMatter.Core.Tests/Security/ContentPathValidatorTests.cs

**Checkpoint**: Foundation ready — user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - Connect a Hugo site repository (Priority: P1) 🎯 MVP

**Goal**: Owner authorizes Hugo Matter via GitHub App and sees their Hugo site as connected

**Independent Test**: Owner with a qualifying Hugo repository completes authorization and sees the site connected; without connection, editing sessions cannot start

### Implementation for User Story 1

- [ ] T028 [US1] Implement connection domain service in src/HugoMatter.Core/Connection/ConnectionService.cs
- [ ] T029 [US1] Implement GET/DELETE /api/connection and POST /api/connection/authorize in src/HugoMatter.ApiService/Endpoints/ConnectionEndpoints.cs per specs/001-hugo-matter-cms/contracts/api-openapi.yaml
- [ ] T030 [US1] Implement GitHub App OAuth/install binding handler in src/HugoMatter.Infrastructure/GitHub/GitHubConnectionHandler.cs
- [ ] T031 [US1] Build Connect repository UI flow in src/HugoMatter.Web/Components/Pages/Connect.razor
- [ ] T032 [US1] Add connected site status and default branch display in src/HugoMatter.Web/Components/Pages/Home.razor
- [ ] T033 [US1] Handle revoked or lost installation authorization with safe failure in src/HugoMatter.Infrastructure/GitHub/GitHubAppClient.cs
- [ ] T034 [P] [US1] Add API handler tests for connection endpoints in tests/HugoMatter.ApiService.Tests/Connection/ConnectionEndpointsTests.cs

**Checkpoint**: User Story 1 fully functional — owner can connect and disconnect a site

---

## Phase 4: User Story 2 - Start a single editing session (Priority: P1)

**Goal**: Owner starts exactly one editing session mapped to a branch and open pull request against the default branch

**Independent Test**: From a connected site with no active session, owner starts one session and sees a corresponding open PR; a second start attempt is refused or guided to the existing session

### Implementation for User Story 2

- [ ] T035 [US2] Implement session creation service (branch + PR) in src/HugoMatter.Core/Sessions/SessionService.cs
- [ ] T036 [US2] Implement GET/POST /api/session in src/HugoMatter.ApiService/Endpoints/SessionEndpoints.cs per specs/001-hugo-matter-cms/contracts/api-openapi.yaml
- [ ] T037 [US2] Enforce one Active session per ConnectedSite in src/HugoMatter.Infrastructure/Persistence/SessionRepository.cs
- [ ] T038 [US2] Build Start editing session UI in src/HugoMatter.Web/Components/Pages/Session.razor
- [ ] T039 [US2] Implement resume-existing-session behavior on return visits in src/HugoMatter.Web/Components/Pages/Session.razor
- [ ] T040 [P] [US2] Add unit tests for single-session enforcement in tests/HugoMatter.Core.Tests/Sessions/SessionServiceTests.cs
- [ ] T041 [P] [US2] Add API tests for session start conflict (409) in tests/HugoMatter.ApiService.Tests/Session/SessionEndpointsTests.cs

**Checkpoint**: User Story 2 fully functional — one session per site with branch + PR

---

## Phase 5: User Story 3 - Create, edit, and delete posts and pages with theme-aware frontmatter (Priority: P1)

**Goal**: Owner creates, edits, and deletes posts/pages with Profile frontmatter fields from the in-product theme pack

**Independent Test**: In an active session on a Profile site, owner creates a post, fills Profile frontmatter, edits body, deletes a content item, and sees changes in the session buffer without hand-editing repo files

### Implementation for User Story 3

- [ ] T042 [P] [US3] Ship Hugo Profile pack definition as embedded resource in src/HugoMatter.ThemePacks/Packs/hugo-profile.json
- [ ] T043 [US3] Implement Profile pack binder and compatibility probe in src/HugoMatter.ThemePacks/Packs/HugoProfileThemePack.cs
- [ ] T044 [US3] Implement in-session content buffer service in src/HugoMatter.Core/Content/ContentBufferService.cs
- [ ] T045 [US3] Implement GET/POST /api/content and GET/PUT/DELETE /api/content/{path} in src/HugoMatter.ApiService/Endpoints/ContentEndpoints.cs
- [ ] T046 [US3] Implement GET /api/theme-packs and GET /api/theme-packs/{id} in src/HugoMatter.ApiService/Endpoints/ThemePackEndpoints.cs
- [ ] T047 [US3] Build content list UI in src/HugoMatter.Web/Components/Pages/Content/ContentList.razor
- [ ] T048 [US3] Build theme-aware content editor in src/HugoMatter.Web/Components/Pages/Content/ContentEditor.razor
- [ ] T049 [US3] Wire dynamic frontmatter field widgets from theme pack schema in src/HugoMatter.Web/Components/Content/ThemeFieldEditor.razor
- [ ] T050 [P] [US3] Add unit tests for Profile pack field application in tests/HugoMatter.Core.Tests/ThemePacks/HugoProfileThemePackTests.cs
- [ ] T051 [P] [US3] Add unit tests for unknown frontmatter preservation in tests/HugoMatter.Core.Tests/Content/ContentBufferServiceTests.cs

**Checkpoint**: User Story 3 fully functional — theme-aware CRUD in session buffer

---

## Phase 6: User Story 4 - Save work to the session pull request (Priority: P1)

**Goal**: Saving persists buffered changes as commits on the session branch/PR

**Independent Test**: After editing, owner saves and the session PR reflects saved content; save failures report clearly without false success

### Implementation for User Story 4

- [ ] T052 [US4] Implement save orchestration (Git commits on session branch) in src/HugoMatter.Core/Sessions/SaveService.cs
- [ ] T053 [US4] Implement POST /api/session/save in src/HugoMatter.ApiService/Endpoints/SessionEndpoints.cs
- [ ] T054 [US4] Add Save action and dirty-state tracking in src/HugoMatter.Web/Components/Pages/Content/ContentEditor.razor
- [ ] T055 [US4] Surface save success and failure messages in src/HugoMatter.Web/Components/Shared/Banner.razor
- [ ] T056 [P] [US4] Add unit tests for save commit flow with mocked GitHub port in tests/HugoMatter.Core.Tests/Sessions/SaveServiceTests.cs
- [ ] T057 [P] [US4] Add API tests for save success and auth failure paths in tests/HugoMatter.ApiService.Tests/Session/SaveEndpointTests.cs

**Checkpoint**: User Story 4 fully functional — durable save to session PR (core MVP loop complete)

---

## Phase 7: User Story 5 - Preview while editing and review with real site preview (Priority: P2)

**Goal**: Owner sees approximate in-editor preview and can start a real Hugo site preview from saved session content only

**Independent Test**: With saved session content, owner uses in-editor preview for the current document and starts full-site preview reflecting saved content; unsaved buffer excluded from site preview

### Implementation for User Story 5

- [ ] T058 [US5] Implement in-editor preview service using Markdig in src/HugoMatter.Core/Preview/EditorPreviewService.cs
- [ ] T059 [US5] Implement POST /api/preview/editor in src/HugoMatter.ApiService/Endpoints/PreviewEndpoints.cs
- [ ] T060 [US5] Implement ISitePreviewOrchestrator with Docker/Podman Hugo container in src/HugoMatter.Infrastructure/Preview/DockerSitePreviewOrchestrator.cs
- [ ] T061 [US5] Implement POST/GET/DELETE /api/preview/site in src/HugoMatter.ApiService/Endpoints/PreviewEndpoints.cs
- [ ] T062 [US5] Build in-editor preview pane in src/HugoMatter.Web/Components/Content/EditorPreviewPane.razor
- [ ] T063 [US5] Build site preview panel with start/stop controls in src/HugoMatter.Web/Components/Pages/Preview/SitePreview.razor
- [ ] T064 [US5] Enforce site preview uses saved session branch tip only in src/HugoMatter.Infrastructure/Preview/DockerSitePreviewOrchestrator.cs
- [ ] T065 [P] [US5] Add unit tests for preview isolation boundaries in tests/HugoMatter.Core.Tests/Preview/SitePreviewOrchestratorTests.cs

**Checkpoint**: User Story 5 fully functional — both preview modes working with correct saved-vs-unsaved semantics

---

## Phase 8: User Story 6 - Multi-language spell checking while editing (Priority: P2)

**Goal**: Owner receives spell-check support for more than one language while editing text

**Independent Test**: Misspellings are indicated for supported languages; owner can switch languages without leaving the editor

### Implementation for User Story 6

- [ ] T066 [US6] Enable browser spellcheck and lang attributes on editor text surfaces in src/HugoMatter.Web/Components/Content/ThemeFieldEditor.razor
- [ ] T067 [US6] Add spell-check language selector component in src/HugoMatter.Web/Components/Content/SpellCheckLanguageSelector.razor
- [ ] T068 [US6] Persist selected spell-check language in editor UI state in src/HugoMatter.Web/Components/Pages/Content/ContentEditor.razor

**Checkpoint**: User Story 6 fully functional — multi-language spell checking in editor

---

## Phase 9: User Story 7 - Publish or discard the editing session (Priority: P2)

**Goal**: Owner publishes (merge PR, delete branch, end session) or discards (close PR, delete branch) with all guard rails

**Independent Test**: From an active session with saved changes, owner publishes and observes merge + branch deletion; alternatively discards with correct guard messaging for unsaved edits, no changes, and merge failures

### Implementation for User Story 7

- [ ] T069 [US7] Implement publish orchestration with guards in src/HugoMatter.Core/Sessions/PublishService.cs
- [ ] T070 [US7] Implement discard orchestration with confirmation guard in src/HugoMatter.Core/Sessions/DiscardService.cs
- [ ] T071 [US7] Implement POST /api/session/publish and POST /api/session/discard in src/HugoMatter.ApiService/Endpoints/SessionEndpoints.cs
- [ ] T072 [US7] Stop active site preview on publish or discard in src/HugoMatter.Infrastructure/Preview/DockerSitePreviewOrchestrator.cs
- [ ] T073 [US7] Build Publish and Discard actions UI with guard messaging in src/HugoMatter.Web/Components/Pages/Session/SessionActions.razor
- [ ] T074 [US7] Add discard confirmation dialog for unsaved edits in src/HugoMatter.Web/Components/Shared/DiscardConfirmDialog.razor
- [ ] T075 [P] [US7] Add unit tests for publish guards (no changes, unsaved edits, merge failure) in tests/HugoMatter.Core.Tests/Sessions/PublishServiceTests.cs
- [ ] T076 [P] [US7] Add unit tests for discard confirmation flow in tests/HugoMatter.Core.Tests/Sessions/DiscardServiceTests.cs

**Checkpoint**: User Story 7 fully functional — editorial loop complete

---

## Phase 10: User Story 8 - Minimal Profile site configuration (Priority: P3)

**Goal**: Owner edits minimal Profile-related site configuration exposed by the theme pack

**Independent Test**: In a session, owner changes allowed config fields, saves to session, and sees them in site preview after save

### Implementation for User Story 8

- [ ] T077 [US8] Implement site configuration read/write in session buffer in src/HugoMatter.Core/Content/SiteConfigService.cs
- [ ] T078 [US8] Implement GET/PUT /api/site-config in src/HugoMatter.ApiService/Endpoints/SiteConfigEndpoints.cs
- [ ] T079 [US8] Build minimal Profile site config editor in src/HugoMatter.Web/Components/Pages/Config/SiteConfigEditor.razor
- [ ] T080 [P] [US8] Add unit tests for site config allowlist validation in tests/HugoMatter.Core.Tests/Content/SiteConfigServiceTests.cs

**Checkpoint**: User Story 8 fully functional — minimal Profile config editable in session

---

## Phase 11: User Story 9 - Ready for additional theme packs later (Priority: P3)

**Goal**: Profile behavior is supplied as an isolatable pack; core flows do not hard-require Profile types

**Independent Test**: Reviewers confirm Profile comes from ThemePacks registry and a second pack can register without redesigning connect → session → edit → preview → publish

### Implementation for User Story 9

- [ ] T081 [US9] Document IThemePackRegistry extension contract in src/HugoMatter.Core/Ports/IThemePackRegistry.cs
- [ ] T082 [US9] Audit and refactor core session/content/preview flows to remove Profile-specific hard-coding in src/HugoMatter.Core/
- [ ] T083 [US9] Add registry unit test proving a second stub pack registers without core changes in tests/HugoMatter.Core.Tests/ThemePacks/ThemePackRegistryTests.cs

**Checkpoint**: User Story 9 complete — theme pack architecture validated for future packs

---

## Phase 12: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

- [ ] T084 [P] Run manual validation scenarios from specs/001-hugo-matter-cms/quickstart.md
- [ ] T085 [P] Add thin Playwright smoke test for connect → edit → save journey in tests/HugoMatter.E2E.Tests/Smoke/EditingWorkflowTests.cs
- [ ] T086 Audit secret handling to ensure credentials are never logged in src/HugoMatter.Infrastructure/GitHub/
- [ ] T087 Align implemented ApiService endpoints with specs/001-hugo-matter-cms/contracts/api-openapi.yaml
- [ ] T088 [P] Remove dead sample code and unused dependencies across src/

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion — BLOCKS all user stories
- **User Stories (Phase 3–11)**: All depend on Foundational phase completion
  - P1 stories (US1–US4) should complete sequentially for MVP: Connect → Session → Content → Save
  - P2 stories (US5–US7) depend on a working save loop (US4) but are independently testable once save exists
  - P3 stories (US8–US9) can proceed after core content/session flows exist
- **Polish (Phase 12)**: Depends on desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational — no dependencies on other stories
- **User Story 2 (P1)**: Depends on US1 (connected site required)
- **User Story 3 (P1)**: Depends on US2 (active session required)
- **User Story 4 (P1)**: Depends on US3 (content buffer to save)
- **User Story 5 (P2)**: Depends on US4 (site preview requires saved session content)
- **User Story 6 (P2)**: Depends on US3 editor surfaces — can parallel with US5 after US4
- **User Story 7 (P2)**: Depends on US4 (publish/discard operate on saved session state)
- **User Story 8 (P3)**: Depends on US3/US4 (session buffer + save)
- **User Story 9 (P3)**: Best after US3/US5/US7 to validate pack isolation across implemented flows

### Within Each User Story

- Domain services before API endpoints
- API endpoints before Blazor UI
- Core implementation before unit tests for that story
- Story complete before moving to next priority

### Parallel Opportunities

- All Setup tasks marked [P] can run in parallel (T002–T004, T007–T009)
- Foundational tasks T012, T017, T025–T027 can run in parallel once prerequisites exist
- After Foundational completes, US6 can parallel with US5 once US4 is done
- US8 and US9 can run in parallel after core P1/P2 flows exist
- Polish tasks T084, T085, T088 can run in parallel

---

## Parallel Example: User Story 3

```bash
# Launch Profile pack and tests together:
Task: "Ship Hugo Profile pack definition in src/HugoMatter.ThemePacks/Packs/hugo-profile.json"
Task: "Add unit tests for Profile pack field application in tests/HugoMatter.Core.Tests/ThemePacks/HugoProfileThemePackTests.cs"

# Launch content API and theme pack API together (different endpoint files):
Task: "Implement content endpoints in src/HugoMatter.ApiService/Endpoints/ContentEndpoints.cs"
Task: "Implement theme pack endpoints in src/HugoMatter.ApiService/Endpoints/ThemePackEndpoints.cs"
```

---

## Parallel Example: User Story 7

```bash
# Launch publish and discard domain services together:
Task: "Implement publish orchestration in src/HugoMatter.Core/Sessions/PublishService.cs"
Task: "Implement discard orchestration in src/HugoMatter.Core/Sessions/DiscardService.cs"

# Launch their unit tests together:
Task: "Add unit tests for publish guards in tests/HugoMatter.Core.Tests/Sessions/PublishServiceTests.cs"
Task: "Add unit tests for discard confirmation in tests/HugoMatter.Core.Tests/Sessions/DiscardServiceTests.cs"
```

---

## Implementation Strategy

### MVP First (User Stories 1–4)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL — blocks all stories)
3. Complete Phase 3: User Story 1 (Connect)
4. Complete Phase 4: User Story 2 (Session)
5. Complete Phase 5: User Story 3 (Content editing)
6. Complete Phase 6: User Story 4 (Save)
7. **STOP and VALIDATE**: Owner can connect → start session → edit Profile post → save → verify PR
8. Demo MVP before adding preview/publish polish

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. US1 → Connect validated
3. US2 → Session + PR validated
4. US3 → Content editing validated
5. US4 → Save validated (MVP!)
6. US5 → Preview validated
7. US6 → Spell check validated
8. US7 → Publish/discard validated (full editorial loop)
9. US8 → Site config validated
10. US9 → Pack extensibility validated
11. Polish → quickstart + E2E smoke

### Parallel Team Strategy

With multiple developers after Foundational:

- Developer A: US1 → US2 (connection + session spine)
- Developer B: US3 → US4 (content + save) — starts once US2 API exists
- Developer C: US5 + US6 (preview + spell check) — starts once US4 exists
- Developer D: US7 → US8 → US9 (publish loop + config + pack audit)

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks in the same batch
- [Story] label maps task to specific user story for traceability
- Each user story should be independently completable and testable at its checkpoint
- Git remains source of truth — no content bodies in SQLite
- Do not test Aspire AppHost modelling (per plan.md)
- Use xUnit v3 + NSubstitute + built-in Assert only; no Moq/FluentAssertions
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
