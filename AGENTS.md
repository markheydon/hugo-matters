# Agent Instructions

These instructions keep agent changes aligned with Hugo Matters' experimental,
local-first CMS direction and existing project workflows.

## Project context

Hugo Matters is an experiment for solo Hugo site owners. It aims to support
editing site content locally, previewing a real Hugo build, and publishing
changes through Git branches and pull requests. It is not a stable product.

Before making a non-trivial change, consult the relevant sources of truth:

- [README.md](README.md) for the project overview and entry points.
- [.specify/memory/constitution.md](.specify/memory/constitution.md) for
  product principles and boundaries.
- The applicable feature documents under [specs/](specs/), especially its
  `spec.md`, `plan.md`, `tasks.md`, and `quickstart.md`.
- The existing implementation and tests for behaviour not specified there.

Do not assume `GOALS.md`, `SCOPE.md`, or `CONVENTIONS.md` exist; project
principles and feature specifications are maintained in the locations above.

## Architecture and quality

- Preserve the local-first model and Git as the source of truth.
- Follow the boundaries and constraints in the constitution and the applicable
  feature plan. Prefer the smallest design that meets the stated requirement.
- Protect repository credentials and other secrets. Never commit or log them;
  validate untrusted paths and content, and keep preview work confined.
- Keep user-facing errors actionable and show staged progress for operations
  comprising multiple steps.
- Add or update tests for behaviour changes, especially around repository
  operations, session lifecycle, preview isolation, and security-sensitive
  paths.
- Use UK English in documentation, comments, and user-facing text.

## Build and validation

The .NET SDK version is pinned in [global.json](global.json). The solution is
[HugoMatters.slnx](HugoMatters.slnx). Follow the checks in
[.github/workflows/ci.yml](.github/workflows/ci.yml):

```sh
dotnet restore HugoMatters.slnx
dotnet format HugoMatters.slnx --no-restore --verify-no-changes --verbosity minimal
dotnet build HugoMatters.slnx --no-restore
dotnet test HugoMatters.slnx --no-build --verbosity normal
```

For local application setup, including the Web CSS build, Aspire startup, and
GitHub App configuration, follow the feature
[quickstart](specs/001-hugo-matter-cms/quickstart.md).

## Agent assets and workflows

Project-specific skills are in `.agents/skills/`; read the matching
`SKILL.md` when its workflow applies.

| Skill | Use when |
|---|---|
| `aspire` and its related skills | Initialising, wiring, deploying, operating, or monitoring the Aspire app |
| `code-review` | Reviewing a branch, pull request, or local change set |
| `create-architectural-decision-record` | Creating or substantially updating an ADR |
| `documentation-writer` and `project-documentation` | Writing or updating project documentation |
| `dotnet-inspect` | Investigating .NET packages, APIs, assemblies, or version differences |
| `playwright-cli` | Running browser workflows or Playwright tests |

`.cursor/skills/` contains the repository's Spec Kit workflows. Cursor-specific
rules are in `.cursor/rules/`, and reusable commands are in
`.cursor/commands/`.

Store ADRs in the repository-root `adr/` directory using
`adr-NNNN-[title-slug].md`; use the ADR skill for their creation or major
updates.

All agent-authored pull requests require human review before merge. Do not
commit, push, or merge unless the user explicitly asks.
