<!--
Sync Impact Report:
- Version change: 1.0.0 → 1.1.0
- Modified principles: none renamed
- Added sections:
  - Core Principle IX. Honest Feedback and Visible Progress
  - Quality Expectations bullets for owner-facing failures and
    compound-operation progress
- Removed sections: none
- Follow-up TODOs: none
-->
# Hugo Matters Constitution

## Core Principles

### I. Security by Default

Secure behavior is the default, not an opt-in.

- MUST use least-privilege access to the owner's repository and related
  platform capabilities.
- MUST protect credentials, tokens, and secrets: never commit them; never
  log them; never embed them in generated site content.
- MUST validate and constrain untrusted input (paths, content, config,
  preview inputs).
- MUST isolate preview and build work so it cannot freely reach beyond its
  intended workspace.
- Private repositories MUST be handled with the same or stricter care as
  public ones.
- Prefer safe failure over silent insecure success.

**Rationale**: Solo owners entrust repository access and secrets to the
product; insecure defaults or silent failure modes create durable harm that
features cannot later paper over.

### II. Git Is Source of Truth

Editorial work MUST flow through the site's Git repository and review
workflow.

- An editing session MUST map to a branch plus a pull request.
- Saving MUST persist as commits on that session.
- Publishing MUST mean merging into the site's main branch.
- The product MUST NOT invent a parallel content store that can diverge from
  the repository as the system of record.

**Rationale**: Owners already trust Git for history and review; a second
store invites drift, lost edits, and unclear publish semantics.

### III. Theme Knowledge Lives in the Product

Theme-specific frontmatter, fields, defaults, and editing behavior MUST be
defined as versioned theme packs inside Hugo Matters—not redeclared in each
owner's site repository.

- The first pack is Hugo Profile.
- Packs MUST be isolatable so additional themes can be added without
  redesigning core editing-session flows.
- Owner repositories remain ordinary Hugo content repositories.

**Rationale**: Centralized, versioned packs keep sites ordinary Hugo trees
and let the product evolve theme UX without requiring owners to maintain
editor schema in-repo.

### IV. Local-First Operation

v1 MUST run as a local tool on the editor's machine.

- Real site preview MUST use an actual Hugo build/serve of the current
  editing session in an isolated local runtime.
- Design boundaries SHOULD allow a future hosted offering.
- v1 MUST NOT depend on multi-tenant hosting, remote build workers, or
  commercial billing.
- Core domain logic MUST NOT be coupled to a specific hosting model.

**Rationale**: Local-first delivers real preview and private-repo safety for
solo owners without forcing early multi-tenant or billing complexity.

### V. Content-First Scope

Primary value is creating and editing posts and pages.

- Limited site configuration is allowed when required for day-one Hugo
  Profile usefulness.
- Broader site plumbing (menus, media libraries, data files, deep Hugo
  advanced features, visual shortcode editing) MUST remain lower priority
  until the content edit → preview → publish loop is solid.

**Rationale**: A reliable content loop proves the product; peripheral site
plumbing dilutes v1 without unlocking the core job.

### VI. Simple Session Model

v1 MUST support a single active editing session per connected site (one
open pull request).

- Concurrent sessions, multi-editor collaboration, and advanced conflict UX
  are future concerns.
- Do not complicate v1 for them unless required for correctness or security.

**Rationale**: One session per site keeps mental model and conflict surface
small while the core loop is proven.

### VII. Faithful Preview and Durable Integrations

- In-editor preview MAY be approximate (for example, shortcodes need not
  render).
- Site preview MUST be a real Hugo build of the session.
- Prefer durable, least-privilege platform integrations for repository
  access over ad-hoc long-lived personal credentials.
- Prefer content round-trip fidelity over editor features that corrupt Hugo
  source.

**Rationale**: Owners judge the product by what the site actually builds and
by whether repository access stays least-privilege and reversible.

### VIII. Simplicity and Clear Boundaries

YAGNI for v1.

- Prefer clear separation between UI, application/services, repository
  integration, theme packs, and preview orchestration.
- Added complexity MUST be justified by a concrete requirement.
- Spec Kit artifacts MUST stay separated: specifications describe what/why
  without an implementation stack; plans carry technology and architecture
  choices.

**Rationale**: Clear boundaries and justified complexity keep the system
evolvable and prevent premature stack or feature lock-in.

### IX. Honest Feedback and Visible Progress

Owners MUST understand failures and in-flight work without guessing.

- User-visible failures MUST name the failed operation in plain language
  and, when known, what the owner can do next.
- Opaque generic copy (for example only "internal error" or "something went
  wrong") MUST NOT be the sole user-visible outcome of a failed operation.
- Internal diagnostics MAY exist for operators; they MUST NOT replace the
  owner-facing explanation.
- User-visible messages MUST NOT disclose secrets, tokens, or other
  sensitive internals (see Security by Default).
- A user-initiated action that comprises two or more distinct steps (local
  or remote) MUST show staged progress through those steps while the work
  runs. An indeterminate busy state on the control that started the action
  is insufficient by itself.
- A single-step action MAY use a simpler busy state.

**Rationale**: Solo owners cannot treat logs or stack traces as the product
UX. Opaque errors stall work; compound operations that look frozen erode
trust even when they succeed.

## Quality Expectations

Critical behaviors MUST be covered by automated tests at the boundaries that
matter:

- Session lifecycle (create, save/commit, publish/merge, abandon)
- Theme-pack application (fields, defaults, isolation across packs)
- Repository operations (branch, commit, pull request, merge semantics)
- Preview isolation (workspace confinement; no unintended reach)
- Owner-facing failure text for operations that can fail in distinguishable
  ways
- Staged progress for user actions that comprise two or more steps

Changes that affect security-sensitive paths MUST include appropriate
automated coverage. Prefer tests that fail when a principle is violated over
tests that only assert happy-path UI chrome.

**Rationale**: Principles are durable only when regressions are detectable;
security and source-of-truth paths are the highest-cost failure modes.
Owner-facing honesty and progress are how those failures stay usable.

## Document Boundaries

This constitution and feature specifications MUST remain technology-agnostic
regarding frameworks, languages, UI toolkits, container runtimes, and
specific cloud or VCS product APIs. Those choices belong in implementation
plans. Domain terms (Hugo, frontmatter, theme, pull request, editing
session) are permitted.

**Rationale**: Keeping stack choices out of governance and specs preserves
principle longevity when implementation details change.

## Governance

This constitution supersedes ad-hoc practice and conflicting guidance in
specs, plans, or tasks.

- Amendments MUST include documented rationale and a Semantic Versioning
  bump (MAJOR for incompatible principle removal/redefinition; MINOR for
  new or materially expanded guidance; PATCH for clarifications and
  non-semantic refinements).
- Implementation plans MUST pass a Constitution Check before execution
  proceeds.
- Unjustified violations MUST be resolved by changing the spec, plan, or
  tasks—not by weakening these principles.

**Version**: 1.1.0 | **Ratified**: 2026-08-21 | **Last Amended**: 2026-08-24
