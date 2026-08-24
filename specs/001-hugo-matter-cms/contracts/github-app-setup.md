# Contract: GitHub App setup (dev)

**Feature**: `001-hugo-matter-cms`  
**Audience**: Developers running Hugo Matters locally

This is an explicit plan/setup contract for day-one GitHub App credentials. PATs are not supported in v1.

## Create the GitHub App

1. GitHub → **Settings** → **Developer settings** → **GitHub Apps** → **New GitHub App**.
2. Suggested name: `Hugo Matters (local)` (or personal variant).
3. **Homepage URL**: local Web URL once known (Aspire dashboard / localhost).
4. **Callback URL**: Web OAuth callback — `https://localhost:<web-port>/auth/callback` (must match `GitHubAuth:HostedSignInCallbackBaseUri` + default callback path).
5. **Webhook**: disable for v1 local loop, or leave inactive (user-driven refresh is enough).
6. **User identification**: enable (email optional).
7. **Permissions** (Repository):
   - Contents: **Read and write**
   - Pull requests: **Read and write**
   - Metadata: **Read-only**
8. **Where can this GitHub App be installed?** → Only on this account (solo owner).
9. Create the app; note **App ID**, **Client ID**, and app **slug** (for install URL).
10. Generate a **Client secret**; generate a **private key** (PEM) and store it outside the repo.

## Local secrets (never commit)

### Primary: Aspire AppHost parameters (recommended)

From the **repository root**, set AppHost user secrets via the Aspire CLI. Keys map to `AddParameter` names in `HugoMatters.AppHost`.

```bash
aspire secret set Parameters:github-app-id "<app-id>"
aspire secret set Parameters:github-app-client-id "<client-id>"
aspire secret set Parameters:github-app-client-secret "<client-secret>"
aspire secret set Parameters:github-app-private-key-pem "<pem-contents-or-absolute-path-to-pem-file>"
aspire secret set Parameters:github-app-callback-base-uri "https://localhost:7175"
```

- `github-app-private-key-pem`: PEM text (include `BEGIN`/`END` lines) **or** an absolute path to a PEM file on the machine running ApiService.
- `github-app-callback-base-uri`: Web HTTPS origin for OAuth callback (e.g. `https://localhost:7175`). Combined with `/auth/callback` for the GitHub App **Callback URL**. Defaults via `appsettings.json` if unset.
- **Web** receives client id/secret and callback base URI as `GitHubAuth__*` environment variables.
- **ApiService** receives App ID and private key PEM as `GitHubApp__*` only (no OAuth client credentials on the API).
- List configured keys (values hidden): `aspire secret list`

Parameters appear in the **Aspire dashboard** under **Parameters**; secret parameters are masked.

### Alternative: per-service dotnet user-secrets

**Web** (OAuth sign-in):

```bash
cd src/HugoMatters.Web
dotnet user-secrets init
dotnet user-secrets set "GitHubAuth:HostedGitHubAppClientId" "<client-id>"
dotnet user-secrets set "GitHubAuth:HostedGitHubAppClientSecret" "<client-secret>"
dotnet user-secrets set "GitHubAuth:HostedSignInCallbackBaseUri" "https://localhost:7175"
```

**ApiService** (installation tokens):

```bash
cd src/HugoMatters.ApiService
dotnet user-secrets init
dotnet user-secrets set "GitHubApp:AppId" "<app-id>"
dotnet user-secrets set "GitHubApp:PrivateKeyPem" "<pem-contents-or-path>"
```

Equivalent environment variables may be used for CI or non-user-secrets hosts. Secrets must never appear in logs, OpenAPI examples with real values, or generated Hugo content.

## Install on the site repository

1. Sign in to Hugo Matters with GitHub (Web establishes the user session).
2. If no installation exists yet, install the App on the owner’s account via the GitHub App install flow.
3. Grant access to the single Hugo site repository (public or private).
4. In the app, enter owner/repo and connect so Hugo Matters records `installationId`, owner, and repo locally.
5. Verify least privilege: App cannot access unrelated repos the owner did not select.

## Runtime token use

- **Web**: short-lived user access tokens in the encrypted auth cookie (for sign-in lifecycle only; not sent to ApiService).
- **ApiService**: exchange installation id for a **short-lived installation access token** per GitHub App docs; use for Contents / Git / Pull Request operations.
- On `401/403`, mark site `AccessLost` and fail safely (no silent success).

## Acceptance checks

- Unauthenticated visitors see welcome/sign-in only, not the CMS shell.
- Private and public repos both connect with the same flow after sign-in.
- Revoking the installation causes subsequent save/publish to fail with a clear message.
- No PAT is required for the happy path.
