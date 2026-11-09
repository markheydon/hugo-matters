# Document Placement Guide

Use this guide after applying the Diátaxis decision from the upstream `documentation-writer` skill.

## Put content in `README.md` when

- It explains what the repository is, why it exists, and how to get started.
- The primary audience is contributors, maintainers, or evaluators of the repository.
- The content needs to be visible immediately on the repository front page.

## Put content in `docs/` when

- The content is end-user-facing or intended to scale beyond a short repository overview.
- It fits a Diátaxis category such as tutorial, how-to guide, reference, or explanation.
- It will grow over time and benefits from navigation, indexing, or cross-linking.

## Put content in another project file when

- It belongs with planning or operational material such as `RUNBOOK.md`, `CONVENTIONS.md`, or `plan/` files.
- It describes internal process, governance, or implementation constraints rather than product or user-facing documentation.

## Decision rules

- Favour `README.md` for orientation and entry points.
- Favour `docs/` for substantial user-facing content.
- Avoid duplicating the same guidance in both places.
- If a README section is becoming long, task-heavy, or reference-like, move the detail into `docs/` and leave a short summary with links.