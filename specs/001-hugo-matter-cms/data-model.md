# Data Model: Hugo Matters CMS

**Feature**: `001-hugo-matter-cms`  
**Date**: 2026-08-21

Local persistence holds **metadata only**. Content entities are representations of Git-backed files for the active session, not a parallel CMS store.

---

## Entities

### Owner

Solo person using the local app. Not a multi-tenant user table in v1.

| Field | Type | Notes |
|-------|------|-------|
| Local identity | implicit | Single local machine user of the app |
| GitHub account login | string | From Web cookie claims after GitHub App user sign-in (`/auth/callback`) |

**Validation**: N/A beyond successful GitHub sign-in. Owner login in claims must match the repository owner when binding a personal repo (org repos use the installation account context).

---

### ConnectedSite

Association between the local app and one GitHub repository.

| Field | Type | Notes |
|-------|------|-------|
| Id | GUID / string | Local primary key |
| InstallationId | long | GitHub App installation id |
| OwnerLogin | string | Repo owner login |
| RepoName | string | Repository name |
| DefaultBranch | string | Cached from GitHub; refresh on connect/session start (publish target) |
| HtmlUrl | string | Convenience |
| ThemePackId | string | e.g. `hugo-profile` |
| ThemePackVersion | string | Pack semver applied |
| ConnectedAt | datetime | |
| Status | enum | `Connected`, `AccessLost`, `Disconnected` |

**Relationships**:
- 0..1 active `EditingSession`
- References one theme pack application

**Validation**:
- Exactly one connected site in v1 (creating a second replaces or is refused — product assumes one site)
- Private and public repos use the same connection model
- On revoked installation: status → `AccessLost`; operations fail safely

---

### EditingSession

Unit of editorial work mapped to one branch + one open PR.

| Field | Type | Notes |
|-------|------|-------|
| Id | GUID / string | Local primary key |
| SiteId | FK | Connected site |
| BranchName | string | e.g. `hugo-matters/session-{shortId}` |
| PullRequestNumber | int | Open PR against default branch |
| PullRequestUrl | string | |
| BaseBranch | string | Repo default branch at session start |
| State | enum | `Active`, `Publishing`, `Discarding`, `Ended` |
| HasUnsavedLocalEdits | bool | UI/server buffer dirty flag |
| CreatedAt | datetime | |
| UpdatedAt | datetime | |

**Relationships**:
- Belongs to one `ConnectedSite`
- At most **one** `Active` session per site (enforced)

**State transitions**:

```text
[none] --start--> Active
Active --save--> Active (commits on branch; dirty=false)
Active --publish (guards pass)--> Publishing --> Ended (merged, branch deleted)
Active --publish (guards fail / merge fail)--> Active (unchanged GitHub PR)
Active --discard (confirm if dirty)--> Discarding --> Ended (PR closed, branch deleted)
```

**Publish guards** (must all pass before merge attempt):
1. `HasUnsavedLocalEdits == false`
2. Session has content changes vs `BaseBranch` / default branch
3. Installation still authorized

**Discard guards**:
1. If `HasUnsavedLocalEdits`, require explicit confirmation after loss warning

**Validation**:
- Cannot start second active session for same site
- Ended sessions are retained only as optional history metadata or deleted; content remains only in Git history

---

### ContentItem

Post or page (body + frontmatter) within a session. **Not** durably stored locally as source of truth; loaded from / written to Git via API.

| Field | Type | Notes |
|-------|------|-------|
| Path | string | Repo-relative path (validated; no `..` escape) |
| ContentType | enum | `Post`, `Page` |
| FrontMatter | ordered map | YAML keys; preserve unknowns |
| Body | string | Markdown body after frontmatter |
| ExistsInSession | bool | false after delete-until-saved semantics as needed |
| IsDeleted | bool | Pending delete in local buffer |
| IsNew | bool | Not yet on branch until save |

**Relationships**:
- Edited under one `EditingSession`
- Field presentation driven by `ThemePack` for the site

**Validation**:
- Paths constrained to allowed content directories for the pack (e.g. `content/posts/`, `content/...`)
- Round-trip: unchanged unknown frontmatter keys preserved
- Delete + save → file removal commit on session branch

---

### ThemePack

Versioned product-owned definition (shipped in app, not owner repo).

| Field | Type | Notes |
|-------|------|-------|
| Id | string | `hugo-profile` |
| Version | semver | |
| DisplayName | string | |
| ContentTypes | list | Post/page definitions |
| Fields | list of `FieldDefinition` | Per content type |
| Defaults | map | Default frontmatter values on create |
| SiteConfigFields | list | Minimal config surface (US8) |
| CompatibilityRules | list | How to detect/support site |

#### FieldDefinition

| Field | Type | Notes |
|-------|------|-------|
| Key | string | Frontmatter key |
| Label | string | Editor label |
| DataType | enum | string, markdown, bool, datetime, list, select, … |
| Required | bool | |
| Default | any | |
| EditorWidget | string | Mapping hint for UI primitive |

**Validation**: Packs isolatable; core session flow must not hard-require Profile types beyond selecting a pack.

---

### SiteConfigurationEdit

Minimal Profile-related configuration values edited in-session and saved as file changes on the session branch (e.g. subsets of `hugo.toml` / params — exact keys defined by Profile pack).

| Field | Type | Notes |
|-------|------|-------|
| Keys | pack-defined | Only exposed fields |
| Values | map | |

**Validation**: Reject unknown keys outside pack allowlist; no full Hugo config IDE.

---

### InEditorPreview

Ephemeral view model — not persisted.

| Field | Type | Notes |
|-------|------|-------|
| Html | string | Approximate render |
| Source | buffer | May include unsaved edits |
| Shortcodes | omitted / literal | Per FR-011 |

---

### SitePreview

Orchestrated real Hugo preview of **saved** session content.

| Field | Type | Notes |
|-------|------|-------|
| Id | string | Preview instance id |
| SessionId | FK | |
| Status | enum | `Starting`, `Running`, `Failed`, `Stopped` |
| BaseUrl | string | localhost URL when running |
| WorkspacePath | string | Isolated directory |
| SourceRef | string | Session branch tip commit SHA used |
| ErrorMessage | string? | Safe failure detail |

**Validation / isolation**:
- Workspace confined; container cannot freely reach beyond mount
- Must use saved branch content only (not dirty buffer)
- Stopped on session end or explicit stop

---

### PublishAction / DiscardAction

Command-style records (audit optional).

**PublishAction** outcomes: `Succeeded` (merged, branch deleted, session Ended), `BlockedNoChanges`, `BlockedUnsavedEdits`, `FailedMerge` (protection/conflict/auth — session remains Active).

**DiscardAction** outcomes: `Succeeded` (PR closed, branch deleted), `Cancelled` (user declined dirty confirmation), `Failed` (API error — session may remain).

---

## Local persistence schema (logical)

Tables/collections (SQLite suggested):

1. `sites` — ConnectedSite rows  
2. `sessions` — EditingSession rows (unique filtered index: one Active per SiteId)  
3. `previews` — optional SitePreview metadata  
4. `secrets` — **not stored in SQLite** if avoidable; use OS secret store / user-secrets / env for App private key and client secret. At most store non-secret installation id + repo ids.

---

## Integrity rules (cross-entity)

1. Git remains SoT for content; local DB never authoritative for post/page bodies.  
2. Active session implies open PR on GitHub; reconciliation on resume if PR closed externally.  
3. Theme pack id on site drives editor fields; switching packs mid-session is out of scope for v1 (Profile only).  
4. Path validation applies to all content read/write/delete operations.
