---
name: code-review
description: >-
  Review Hugo Matters changes for correctness, security, architecture, scope,
  and maintainability. Use when asked to review a branch, pull request, or
  local diff. Read-only unless the user explicitly asks for fixes.
---

# Code review

Review the change set as a senior engineer deciding whether it is ready for
human review. Report only actionable issues that could materially affect
correctness, security, scope, or maintainability. Do not make changes unless
the user explicitly asks.

## Scope

- For a named pull request or branch, review the changes against its target
  branch.
- For an explicitly local or uncommitted review, review staged and unstaged
  changes only.
- Otherwise, review the branch changes from the merge base with the repository
  default branch, including committed and uncommitted changes.
- If there is no relevant diff, say so briefly and stop.

## Review focus

1. Bugs, incorrect behaviour, and unsafe failure handling.
2. Security and privacy, especially secrets, repository access, untrusted
   paths or content, and preview isolation.
3. Alignment with the Hugo Matters constitution and applicable feature
   specification and plan.
4. Architectural boundaries, unnecessary complexity, and maintainability.
5. Missing or inadequate tests when behaviour changes.
6. User-facing errors and staged progress for multi-step operations.

Read the changed code and enough surrounding context to verify each finding.
Do not report style preferences, speculative future concerns, or unrelated
pre-existing issues.

## Output

Lead with findings ordered by severity. For each finding, include its severity,
file and line, why it matters, and a concrete improvement. Use UK English.

| Severity | Location | Why it matters | Suggested improvement |
|---|---|---|---|
| … | … | … | … |

If there are no actionable findings, say so explicitly and briefly state any
material validation gap. Do not claim checks were run unless they were.

## After review

Remain read-only unless the user asks for fixes. When asked to fix findings,
make minimal changes that follow project patterns and run the relevant checks
from the repository CI workflow.
