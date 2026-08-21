# Feature Specification: Hugo Matter CMS

**Feature Branch**: `001-hugo-matter-cms`

**Created**: 2026-08-21

**Status**: Draft

**Input**: User description: "Build Hugo Matter: a CMS that lets a solo site owner edit their Hugo website without hand-editing files in their repository."

## Clarifications

### Session 2026-08-21

- Q: When the owner discards an editing session, what should happen to that session’s open pull request and branch? → A: Close the pull request without merging and delete the session branch
- Q: If the owner tries to publish an editing session that has no content changes relative to the main branch, what should happen? → A: Block publish when there are no changes vs main; show a clear message
- Q: If the owner discards a session while they still have unsaved local edits, what should happen? → A: Warn that unsaved edits will be lost; discard only after explicit confirmation
- Q: If publish cannot merge because the main branch is protected or otherwise blocked (for example required reviews or checks), what should Hugo Matter do? → A: Fail publish with a clear explanation; leave the session and open pull request intact for the owner to resolve
- Q: Should the real Hugo site preview include unsaved local edits, or only content already saved to the session pull request? → A: Site preview uses only content already saved to the session pull request; unsaved edits are omitted until saved
- Q: If the owner tries to publish while they still have unsaved local edits, what should happen? → A: Block publish until unsaved edits are saved or reverted; explain why
- Q: After a successful publish (merge into main), should Hugo Matter delete the session branch? → A: After successful merge, delete the session branch (session ends cleanly)
- Q: If the main branch has moved ahead and the session can no longer merge cleanly (conflict), what should Hugo Matter do? → A: Fail publish with a clear conflict explanation; leave session and open pull request intact for the owner to resolve
- Q: When the spec says publishing merges into the site’s “main” branch, which branch should that be if the repository’s default branch has a different name? → A: Use the repository’s configured default branch as the publish/integration target (whatever it is named)
- Q: In this initial product, can the owner delete existing posts or pages from within an editing session? → A: Yes — owners can delete posts/pages in the session; deletes save to the session like other edits

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Connect a Hugo site repository (Priority: P1)

A solo site owner authorizes Hugo Matter to access their Hugo site's GitHub-hosted repository (public or private). After a successful connection, they can start editing without manually cloning or hand-editing files in the repository.

**Why this priority**: Nothing else works until the product can securely reach the owner's content repository under the owner's control.

**Independent Test**: An owner with a qualifying Hugo repository can complete authorization and see the site as connected; without connection, editing sessions cannot start.

**Acceptance Scenarios**:

1. **Given** an owner has a Hugo site in a GitHub repository they control, **When** they complete the secure authorization flow granting repository access, **Then** the product records a successful connection and the owner can proceed to start an editing session.
2. **Given** an owner starts connection for a private repository, **When** they authorize access, **Then** the product gains only the least privilege needed for editing and publishing through the repository workflow, and private content is treated with at least the same care as public content.
3. **Given** an owner cancels or denies authorization, **When** the flow ends, **Then** the product does not gain repository access and does not present the site as connected.
4. **Given** a connected site, **When** the owner later revokes authorization through the platform they control, **Then** subsequent repository operations fail safely and the product does not silently succeed with stale access.

---

### User Story 2 - Start a single editing session (Priority: P1)

Once connected, the owner starts an editing session. The session appears as exactly one open pull request against the site's default (integration) branch. Only one active editing session is allowed per connected site.

**Why this priority**: The session is the unit of work for all edits, saves, previews, and publish/discard actions; the single-session rule keeps the mental model simple.

**Independent Test**: From a connected site with no active session, the owner can start one session and observe a corresponding open pull request; attempting to start another while one is active is refused or guided to the existing session.

**Acceptance Scenarios**:

1. **Given** a connected site with no active editing session, **When** the owner starts a session, **Then** an editing session is created that maps to a branch and a single open pull request against the repository's default branch.
2. **Given** a connected site that already has an active editing session, **When** the owner tries to start another session, **Then** the product does not create a second concurrent session and instead keeps the owner on the existing one (or clearly indicates only one is allowed).
3. **Given** an active editing session, **When** the owner returns later, **Then** they can resume that same session and continue editing against the same pull request.

---

### User Story 3 - Create, edit, and delete posts and pages with theme-aware frontmatter (Priority: P1)

Within an active session, the owner creates, edits, and deletes posts and pages, including Hugo frontmatter. For the Hugo Profile theme, the product already knows the fields Profile needs and presents an appropriate editing experience—the owner does not configure that field schema inside their repository.

**Why this priority**: Creating, editing, and deleting content is the primary value of the product; theme-aware frontmatter is required for day-one Profile usefulness.

**Independent Test**: In an active session on a Profile-based site, the owner can create a post, fill Profile frontmatter fields presented by the product, edit body content, delete a content item, and see those changes as part of the session without editing raw repository files by hand.

**Acceptance Scenarios**:

1. **Given** an active session on a Hugo Profile site, **When** the owner creates a new post or page, **Then** they can enter title, body, and Profile-relevant frontmatter through the product's editing experience without defining field schemas in their repository.
2. **Given** an existing post or page in the site content, **When** the owner opens it in the session, **Then** they can edit content and frontmatter and the product preserves round-trip fidelity of Hugo source (no silent corruption of structure or fields the owner did not change).
3. **Given** theme knowledge lives in the product as a theme pack, **When** the owner edits Profile content, **Then** defaults and field presentation come from the Hugo Profile pack, not from per-site schema files the owner maintains for the CMS.
4. **Given** an existing post or page in the session, **When** the owner deletes it and saves, **Then** the deletion is persisted on the session pull request like other session edits.

---

### User Story 4 - Save work to the session pull request (Priority: P1)

Saving persists the owner's changes onto the editing session's pull request so work is durable in the repository workflow, not only in local memory.

**Why this priority**: Without durable save, edits are unsafe and publishing cannot be trustworthy.

**Independent Test**: After editing, the owner saves and can verify the session's pull request reflects the saved content; abandoning the local tool without saving does not invent a separate durable content store outside the repository.

**Acceptance Scenarios**:

1. **Given** unsaved edits in an active session, **When** the owner saves, **Then** changes are persisted as commits on the session's branch/pull request.
2. **Given** a successful save, **When** the owner or another viewer inspects the open pull request, **Then** the saved content is visible there as the system of record for the session.
3. **Given** a save failure (for example, lost authorization or repository rejection), **When** the operation cannot complete, **Then** the product reports a clear failure and does not claim the session is safely updated.

---

### User Story 5 - Preview while editing and review with real site preview (Priority: P2)

While editing, the owner sees an in-editor preview of the content (Hugo shortcodes need not render there). Separately, they can open a site preview that builds and serves the full site with real Hugo from content already saved to the session pull request.

**Why this priority**: Preview confidence is essential before publish, but the core edit/save loop can be validated first; both preview modes complete the day-one quality bar.

**Independent Test**: With saved session content present, the owner can use in-editor preview for the current document and can start a full-site preview that reflects saved session content as a real Hugo-built site (unsaved buffer excluded).

**Acceptance Scenarios**:

1. **Given** the owner is editing a post or page, **When** they view in-editor preview, **Then** they see an approximate rendering of the content suitable for layout/readability checks, even if shortcodes do not render.
2. **Given** an active session with saved session content, **When** the owner opens site preview, **Then** the product builds and serves the full site with real Hugo using only content already saved to the session pull request so they can review before publishing.
3. **Given** site preview is running, **When** the owner reviews pages affected by their saved edits, **Then** what they see matches a real Hugo build of the saved session rather than a purely approximate mock of the whole site.
4. **Given** the owner has unsaved local edits, **When** they open site preview without saving, **Then** those unsaved edits do not appear in the site preview until they are saved to the session.

---

### User Story 6 - Multi-language spell checking while editing (Priority: P2)

While editing text, the owner receives spell-checking support for more than one language so multilingual content can be corrected without leaving the editor.

**Why this priority**: Improves content quality during editing; not required to prove connect → session → save, but part of the stated day-one editing experience.

**Independent Test**: With body or frontmatter text open for edit, misspellings are indicated for supported languages the owner is writing in (or has selected), and corrections can be applied without leaving the editing flow.

**Acceptance Scenarios**:

1. **Given** the owner is editing content in a supported language, **When** they enter misspelled words, **Then** the product indicates spelling issues and offers corrections.
2. **Given** the owner works with more than one language across content, **When** they edit, **Then** spell checking remains available for multiple languages rather than being limited to a single fixed language only.

---

### User Story 7 - Publish or discard the editing session (Priority: P2)

Publishing merges the session's pull request into the repository's default branch, deletes the session branch, and ends the session. The live site is expected to update through the repository's existing deploy/publish process. Cancelling or discarding a session closes that pull request without merging and deletes the session branch.

**Why this priority**: Completes the editorial loop; depends on a working session with savable content.

**Independent Test**: From an active session with saved changes, the owner can publish and observe the pull request merged into the default branch with the session branch deleted; alternatively they can discard and observe the session end without merge.

**Acceptance Scenarios**:

1. **Given** an active session with changes the owner wants live, **When** they publish, **Then** the session's pull request is merged into the repository's default branch, the session branch is deleted, and the editing session is no longer active.
2. **Given** a successful publish, **When** the repository's existing deploy/publish process runs, **Then** the live site updates through that existing process—the product does not invent a new hosting or deploy product.
3. **Given** an active session the owner no longer wants, **When** they cancel or discard the session, **Then** the pull request is closed without merging, the session branch is deleted, and the session ends so a new session can be started later.
4. **Given** publish cannot complete (authorization lost, merge blocked by branch protection or required checks/reviews, merge conflict with main, or similar), **When** the owner attempts to publish, **Then** the product fails safely with a clear explanation, leaves the session and open pull request intact for the owner to resolve, and does not present a half-published or falsely successful state.
5. **Given** an active session with no content changes relative to the repository's default branch, **When** the owner attempts to publish, **Then** publish is blocked and the product explains that there is nothing to publish.
6. **Given** an active session with unsaved local edits, **When** the owner chooses discard, **Then** the product warns that unsaved edits will be lost and proceeds with discard only after explicit confirmation.
7. **Given** an active session with unsaved local edits, **When** the owner attempts to publish, **Then** publish is blocked until those edits are saved or reverted, and the product explains why.

---

### User Story 8 - Minimal Profile site configuration (Priority: P3)

The owner can edit the small set of site configuration needed for Hugo Profile day-one usefulness, without a full configuration IDE or deep Hugo advanced feature surface.

**Why this priority**: Useful for Profile sites but secondary to the content edit → preview → publish loop.

**Independent Test**: In a session, the owner can change the minimal Profile-related configuration fields the product exposes, save them to the session, and see them reflected in site preview and after publish as appropriate.

**Acceptance Scenarios**:

1. **Given** an active session on a Profile site, **When** the owner opens site configuration editing, **Then** they see only the limited configuration needed for day-one Profile usefulness—not a full site plumbing IDE.
2. **Given** the owner changes an allowed configuration value, **When** they save, **Then** the change persists on the session pull request like other session edits.

---

### User Story 9 - Ready for additional theme packs later (Priority: P3)

The product's connect → session → edit → preview → publish experience is structured so additional theme packs can be added later without redesigning that core flow. Hugo Profile is the first pack.

**Why this priority**: Protects future expansion; day-one delivery still centers on Profile, but the experience must not hard-wire Profile as an irreplaceable core flow.

**Independent Test**: Reviewers can confirm Profile behavior is supplied as an isolatable theme pack and that session/connect/preview/publish flows do not require Profile-specific redesign to conceptualize a second pack.

**Acceptance Scenarios**:

1. **Given** Hugo Profile is the day-one theme pack, **When** the product applies theme-specific editing behavior, **Then** that behavior comes from the Profile pack rather than from one-off logic that cannot be separated from the core session flow.
2. **Given** a future additional theme pack, **When** it is introduced, **Then** owners still use the same connect → session → edit → preview → publish experience without that flow being redesigned around Profile alone.

---

### Edge Cases

- Owner denies, cancels, or later revokes repository authorization mid-session.
- Repository is private vs public (same or stricter care; no weaker path for private).
- Active session already exists when the owner tries to start another.
- Save or publish fails due to lost access, merge conflicts, or repository policy blocks (including protected default branch / required reviews or checks): publish fails safely with a clear explanation; session and open pull request remain for the owner to resolve; no false success. When the default branch has moved ahead such that the session cannot merge cleanly, the same fail-and-keep-session behavior applies (no in-product conflict editor in v1).
- Owner discards a session after partial saves (pull request closed without merge; session branch deleted; no parallel content store retains the edits as published).
- Owner discards with unsaved local edits: product warns that unsaved edits will be lost and requires explicit confirmation before closing the PR and deleting the branch.
- Session content includes Hugo shortcodes (in-editor preview may not render them; site preview must still use real Hugo).
- Site preview vs unsaved buffer: site preview reflects only content saved to the session pull request; unsaved local edits appear in site preview only after save (in-editor preview may still show the live buffer).
- Owner edits only frontmatter, only body, or both; unchanged content must not be corrupted.
- Owner deletes a post or page: deletion persists on the session when saved; publish merges the deletion; discard drops the deletion with the session.
- Site is not Hugo Profile–compatible: product should fail clearly rather than silently mis-apply Profile fields (assume day-one target is Profile; non-Profile sites are outside happy-path success).
- Network interruption during connect, save, or publish: no silent success; owner can retry or discard safely.
- Empty session publish (no content changes relative to the default branch): publish is blocked with a clear message; the session remains active until the owner edits, discards, or otherwise ends it.
- Publish with unsaved local edits: publish is blocked until the owner saves or reverts those edits; the product explains why.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Product MUST allow a solo owner to connect exactly one Hugo site whose content lives in a GitHub-hosted Git repository (public or private) through a secure authorization the owner controls.
- **FR-002**: Product MUST use least-privilege access to the connected repository and MUST protect credentials, tokens, and secrets (never commit them; never log them; never embed them in generated site content).
- **FR-003**: Product MUST validate and constrain untrusted input related to paths, content, configuration, and preview.
- **FR-004**: Product MUST allow the owner to start an editing session that maps to a branch plus a single open pull request against the repository's default branch (the integration/publish target, regardless of whether it is named `main`).
- **FR-005**: Product MUST allow only one active editing session per connected site at a time.
- **FR-006**: Product MUST NOT invent a parallel content store that can diverge from the Git repository as the system of record.
- **FR-007**: Within a session, owners MUST be able to create, edit, and delete posts and pages, including Hugo frontmatter; deletions MUST persist to the session pull request when saved, like other edits.
- **FR-008**: For Hugo Profile, theme-specific frontmatter fields, defaults, and editing presentation MUST come from a versioned theme pack inside the product—not from schemas the owner maintains in their repository for the CMS.
- **FR-009**: Theme packs MUST be isolatable so additional themes can be added later without redesigning the core connect → session → edit → preview → publish flow.
- **FR-010**: Owner repositories MUST remain ordinary Hugo content repositories (no requirement that owners install CMS-specific schema as the source of theme knowledge).
- **FR-011**: Product MUST provide an in-editor content preview while editing; shortcodes MAY omit rendering in that pane.
- **FR-012**: Product MUST provide multi-language spell checking while the owner is editing text.
- **FR-013**: Product MUST provide a separate site preview that builds and serves the full site with real Hugo for the current editing session in an isolated local runtime that cannot freely reach beyond its intended workspace; that site preview MUST reflect only content already saved to the session pull request (unsaved local edits MUST NOT appear until saved).
- **FR-014**: Saving MUST persist session changes as commits on the session's pull request/branch.
- **FR-015**: Publishing MUST mean merging the session's pull request into the repository's default branch; on successful merge, the product MUST delete the session branch and end the editing session.
- **FR-016**: After publish, the live site is expected to update through the repository's existing deploy/publish process; inventing a new hosting product is out of scope.
- **FR-017**: Cancelling or discarding a session MUST close that pull request without merging and MUST delete the session branch.
- **FR-018**: Product MUST support minimal editing of site configuration required for Hugo Profile day-one usefulness, without offering a full configuration IDE or deep Hugo advanced features.
- **FR-019**: Product MUST prefer safe failure over silent insecure or falsely successful outcomes for connect, save, preview, publish, and discard.
- **FR-020**: Product MUST prefer content round-trip fidelity over editing features that would corrupt Hugo source.
- **FR-021**: Initial product MUST run as a local tool on the editor's machine and MUST NOT depend on multi-tenant hosting, remote build workers, or commercial billing for its core loop.
- **FR-022**: Product MUST refuse or safely block scope outside this specification: multi-user collaboration, multiple concurrent sessions/pull requests, full visual shortcode editing, menus/media libraries/data files/deep Hugo advanced features beyond posts/pages plus minimal Profile-related configuration, and non-GitHub repository hosts.
- **FR-023**: Product MUST block publish when the session has no content changes relative to the repository's default branch and MUST explain that there is nothing to publish.
- **FR-024**: When discard is requested with unsaved local edits, the product MUST warn that those edits will be lost and MUST require explicit confirmation before closing the pull request and deleting the session branch.
- **FR-025**: When publish cannot merge because the default branch is protected or otherwise blocked (including required reviews or checks), or because the session cannot merge cleanly due to conflicts with the default branch, the product MUST fail safely with a clear explanation and MUST leave the editing session and open pull request intact for the owner to resolve—MUST NOT report publish success and MUST NOT provide an in-product conflict-resolution editor in this initial product.
- **FR-026**: When publish is requested with unsaved local edits, the product MUST block publish until those edits are saved or reverted and MUST explain why.
- **FR-027**: The publish/integration target MUST be the repository's configured default branch, regardless of whether that branch is named `main`.

### Key Entities

- **Owner**: Solo person who controls the Hugo site repository and authorizes product access.
- **Connected Site**: Association between the owner and one Hugo repository used as the content source of truth.
- **Editing Session**: Unit of editorial work mapped to one branch and one open pull request against the repository's default branch; at most one active per connected site.
- **Content Item**: A post or page (body plus frontmatter) that can be created, edited, or deleted within a session.
- **Theme Pack**: Versioned product-owned definition of theme-specific fields, defaults, and editing behavior (Hugo Profile is the first pack).
- **In-Editor Preview**: Approximate rendering of the content currently being edited (shortcodes need not render).
- **Site Preview**: Real Hugo build/serve of the full site for the current session, isolated to its intended workspace, based only on content already saved to the session pull request.
- **Publish Action**: Merge of the session pull request into the repository's default branch (only when there are changes vs that branch and no unsaved local edits), then delete the session branch and end the session; existing repository deploy/publish processes may update the live site afterward.
- **Discard Action**: End of session that closes the pull request without merging and deletes the session branch. When unsaved local edits exist, discard requires an explicit confirmation after a loss warning.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A qualifying owner can connect a Hugo Profile–based GitHub repository and reach a ready-to-edit state in under 10 minutes on first use (excluding time spent creating accounts on external platforms they already use).
- **SC-002**: From a connected site, an owner can start a session, create or edit at least one post with Profile frontmatter, save, and confirm the session pull request reflects those changes without hand-editing repository files—completable in under 15 minutes for a simple post.
- **SC-003**: 100% of successful saves result in durable session history on the session pull request; no successful save relies on a parallel content store as source of truth.
- **SC-004**: Owners can open in-editor preview for the current document and, separately, a real Hugo site preview of saved session content before publishing; site preview reflects saved session content for reviewed pages and omits unsaved local edits.
- **SC-005**: Spell checking flags misspellings during editing for more than one supported language in the day-one experience.
- **SC-006**: Owners can publish a session (merge to the default branch, then delete the session branch) or discard it (close without merge and delete the session branch) as an explicit choice; after either successful end path, the session branch is gone and no unintended merge remains from discard.
- **SC-007**: At least 90% of first-time test owners complete the connect → edit → save → site-preview → publish path successfully without needing to understand the product's internal implementation stack.
- **SC-008**: Attempts to start a second concurrent editing session on the same connected site never result in two active sessions.
- **SC-009**: Private and public repositories both complete the core loop; private repositories never receive weaker credential or access handling than public ones.
- **SC-010**: Minimal Profile-related site configuration that the product exposes can be changed, saved to the session, and included in site preview without requiring a full configuration IDE.
- **SC-011**: Publish with no changes relative to the default branch is refused with a clear explanation; the session stays active.
- **SC-012**: Discard with unsaved local edits always shows a loss warning and never completes without explicit confirmation.
- **SC-013**: When merge to the default branch is blocked by protection, required reviews/checks, or merge conflicts, publish is reported as failed with an explanation, and the session remains active with its open pull request.
- **SC-014**: Opening site preview without saving never shows unsaved local edits in the real Hugo site preview.
- **SC-015**: Publish with unsaved local edits is refused until the owner saves or reverts; the product always explains why.
- **SC-016**: After every successful publish, the session branch is deleted and the editing session is ended.
- **SC-017**: Connect/session/publish work when the repository default branch is not named `main`, using that default as the integration target.
- **SC-018**: An owner can delete an existing post or page in a session, save that deletion to the session pull request, and see it reflected after publish (or dropped if the session is discarded).

## Assumptions

- The day-one target site uses the Hugo Profile theme (or is compatible with the Hugo Profile theme pack).
- The owner already has (or can obtain) control of a GitHub-hosted Git repository containing their Hugo site content and can complete platform authorization.
- The repository's configured default branch is the integration branch that publishing merges into (it need not be named `main`).
- Merge conflicts with the default branch are resolved outside Hugo Matter for this initial product; the product fails publish clearly and keeps the session open.
- After merge to the default branch, any live-site update happens through the owner's existing repository deploy/publish process; Hugo Matter does not operate hosting.
- The initial product is used by one owner on one connected site; multi-site portfolios are out of scope unless later specified.
- "Multi-language spell checking" means spell checking is available for more than one language during editing (language selection or detection details are left to planning).
- In-editor preview is intentionally approximate and may show the live editing buffer; real Hugo site preview reflects only content saved to the session pull request.
- Non-Profile themes are not part of the day-one happy path; clear failure or unsupported messaging is acceptable until additional theme packs exist.
- Menus, media libraries, data files, visual shortcode editing, collaboration, concurrent sessions, non-GitHub hosts, multi-tenant hosting, and remote build workers remain out of scope for this specification.
- Local-first operation applies to v1 as ratified in the project constitution; design may allow a future hosted offering without depending on it now.
