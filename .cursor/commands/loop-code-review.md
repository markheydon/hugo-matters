# Looped code review

Use this workflow when asked to review and resolve actionable findings in the
current change set.

1. **Review** — Follow
   [the code-review skill](../../.agents/skills/code-review/SKILL.md) and
   review the diff appropriate to the user's scope. Keep the review read-only.
2. **Fix** — Address actionable findings that are in scope. Use minimal
   changes and do not silently expand the requested work.
3. **Verify** — Run the relevant checks from
   [the CI workflow](../../.github/workflows/ci.yml), including restore,
   formatting, build, and tests when applicable. Fix failures caused by the
   changes before another review cycle.
4. **Repeat** — Review the updated diff. Stop when no actionable findings
   remain or after six complete cycles; report any unresolved findings and
   explain why they remain.

Do not commit, push, or merge unless the user explicitly asks.
