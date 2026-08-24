# Hugo Matters

> **Early idea — proceed with caution**
>
> Hugo Matters is an experiment in progress. There is no stable release, no guarantee it will ever ship, and the direction may change or be abandoned entirely. Treat everything here as design notes and exploratory code, not a product you can depend on.

Hugo Matters is a local-first CMS for solo Hugo site owners. It connects to your GitHub repository, runs editing sessions on branches with pull requests, and keeps Git as the source of truth for all content.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (version pinned in [`global.json`](global.json))
- [Aspire CLI](https://aspire.dev/get-started/aspire-cli/) for local orchestration

## Documentation

- Feature specification and implementation plan: [`specs/001-hugo-matter-cms/`](specs/001-hugo-matter-cms/)
- Validation guide: [`specs/001-hugo-matter-cms/quickstart.md`](specs/001-hugo-matter-cms/quickstart.md)
- Project constitution: [`.specify/memory/constitution.md`](.specify/memory/constitution.md)

## Licence

This project is licensed under the MIT License — see [`LICENSE`](LICENSE).
