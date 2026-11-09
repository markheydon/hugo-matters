---
name: project-documentation
description: >-
  Project-aware documentation conventions for the Hugo Matters README, docs,
  feature specifications, and other project documentation.
---

# Project documentation companion

Use this skill with `documentation-writer` to ensure documentation reflects
the repository's actual structure, terminology, and behaviour.

## Workflow

1. Read the relevant repository context:
   - `AGENTS.md` for repository guidance and workflows.
   - `.specify/memory/constitution.md` for product principles and boundaries.
   - The applicable feature documents under `specs/`.
   - Existing documentation and implementation for current behaviour.
2. Choose a location using
   [the document placement guide](references/document-placement.md).
3. Use a starter from `assets/` when it helps.
4. Review the result with
   [the documentation checklist](references/review-checklist.md).

Do not assume `GOALS.md`, `SCOPE.md`, or `CONVENTIONS.md` exist. Ground
terminology and recommendations in available sources; flag conflicts instead
of silently choosing between them.

## Scope boundaries

- Use this skill for project documentation, not ADR authoring.
- For an ADR, use `create-architectural-decision-record`.
- Avoid duplicating the same guidance in multiple documents.
- Keep claims aligned with Hugo Matters' experimental status and implemented
  behaviour.

## References

- [Document placement](references/document-placement.md)
- [Documentation checklist](references/review-checklist.md)
- [README section starter](assets/readme-section-template.md)
- [How-to starter](assets/how-to-template.md)
- [Tutorial starter](assets/tutorial-template.md)
- [Technical blog starter](assets/technical-blog-template.md)
