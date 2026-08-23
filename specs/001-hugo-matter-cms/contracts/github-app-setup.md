# Contract: GitHub App setup (dev)

**Feature**: `001-hugo-matter-cms`  
**Audience**: Developers running Hugo Matters locally

This is an explicit plan/setup contract for day-one GitHub App credentials. PATs are not the primary auth model.

## Create the GitHub App

1. GitHub → **Settings** → **Developer settings** → **GitHub Apps** → **New GitHub App**.
2. Suggested name: `Hugo Matters (local)` (or personal variant).
3. **Homepage URL**: local Web URL once known (Aspire dashboard / localhost).
4. **Callback URL**: ApiService (or Web) OAuth callback route, e.g. `https://localhost:<api-port>/api/connection/callback` (exact path finalized in implementation; must match OpenAPI authorize flow).
5. **Webhook**: disable for v1 local loop, or leave inactive (user-driven refresh is enough).
6. **Permissions** (Repository):
   - Contents: **Read and write**
   - Pull requests: **Read and write**
   - Metadata: **Read-only**
7. **Where can this GitHub App be installed?** → Only on this account (solo owner).
8. Create the app; note **App ID**, **Client ID**.
9. Generate a **Client secret**; generate a **private key** (PEM) and store it outside the repo.

## Local secrets (never commit)

### Primary: Aspire AppHost parameters (recommended)

From the **repository root**, set AppHost user secrets via the Aspire CLI. Keys map to `AddParameter` names in `HugoMatters.AppHost` and are injected into ApiService as `GitHubApp__*` environment variables.

```bash
aspire secret set Parameters:github-app-id "<app-id>"
aspire secret set Parameters:github-app-client-id "<client-id>"
aspire secret set Parameters:github-app-client-secret "<client-secret>"
aspire secret set Parameters:github-app-private-key-pem "<pem-contents-or-absolute-path-to-pem-file>"
aspire secret set Parameters:github-app-redirect-uri "http://localhost:5253/connect"
```

- `github-app-private-key-pem`: PEM text (include `BEGIN`/`END` lines) **or** an absolute path to a PEM file on the machine running ApiService.
- `github-app-redirect-uri`: optional; defaults to `http://localhost:5253/connect` if unset. Set via `aspire secret set` when the Web app runs on a different port or scheme (e.g. `https://localhost:7175/connect`). Must match the GitHub App **Callback URL** (Web `/connect` route on the local Web port). Do not use `publishValueAsDefault` for this parameter—Aspire secrets override the code default only when that flag is absent.
- List configured keys (values hidden): `aspire secret list`

Parameters appear in the **Aspire dashboard** under **Parameters**; secret parameters are masked.

### Alternative: ApiService dotnet user-secrets

For running ApiService without AppHost (e.g. isolated API debugging):

```bash
cd src/HugoMatters.ApiService
dotnet user-secrets init
dotnet user-secrets set "GitHubApp:AppId" "<app-id>"
dotnet user-secrets set "GitHubApp:ClientId" "<client-id>"
dotnet user-secrets set "GitHubApp:ClientSecret" "<client-secret>"
dotnet user-secrets set "GitHubApp:PrivateKeyPem" "<pem-contents-or-path>"
dotnet user-secrets set "GitHubApp:RedirectUri" "http://localhost:5253/connect"
```

Equivalent environment variables may be used for CI or non-user-secrets hosts. Secrets must never appear in logs, OpenAPI examples with real values, or generated Hugo content.

## Install on the site repository

1. Install the App on the owner’s account.
2. Grant access to the single Hugo site repository (public or private).
3. Complete the in-app connect flow so Hugo Matters records `installationId`, owner, and repo.
4. Verify least privilege: App cannot access unrelated repos the owner did not select.

## Runtime token use

- Exchange installation id for a **short-lived installation access token** per GitHub App docs.
- Use that token for Contents / Git / Pull Request operations.
- On `401/403`, mark site `AccessLost` and fail safely (no silent success).

## Acceptance checks

- Private and public repos both connect with the same flow.
- Revoking the installation causes subsequent save/publish to fail with a clear message.
- No PAT is required for the happy path.
