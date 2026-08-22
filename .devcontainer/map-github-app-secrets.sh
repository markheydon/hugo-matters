#!/usr/bin/env bash
# Maps Codespaces / devcontainer HM_* environment variables to HugoMatters.ApiService
# user-secrets (see specs/001-hugo-matter-cms/contracts/github-app-setup.md).
#
# Set repository or user secrets in GitHub Codespaces, for example:
#   HM_GITHUB_APP_ID
#   HM_GITHUB_APP_CLIENT_ID
#   HM_GITHUB_APP_CLIENT_SECRET
#   HM_GITHUB_APP_PRIVATE_KEY_PEM

set -euo pipefail

APISERVICE="src/HugoMatters.ApiService/HugoMatters.ApiService.csproj"

map_secret() {
  local env_name="$1"
  local secret_name="$2"
  local value="${!env_name:-}"

  if [[ -n "$value" ]]; then
    dotnet user-secrets set "$secret_name" "$value" --project "$APISERVICE"
    echo "Mapped ${env_name} -> ${secret_name}"
  fi
}

map_secret HM_GITHUB_APP_ID "GitHubApp:AppId"
map_secret HM_GITHUB_APP_CLIENT_ID "GitHubApp:ClientId"
map_secret HM_GITHUB_APP_CLIENT_SECRET "GitHubApp:ClientSecret"
map_secret HM_GITHUB_APP_PRIVATE_KEY_PEM "GitHubApp:PrivateKeyPem"
