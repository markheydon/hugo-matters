#!/usr/bin/env bash
set -euo pipefail

export PATH="$HOME/.local/bin:$PATH"

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
init_options_path="$repo_root/.specify/init-options.json"

if ! command -v uv >/dev/null 2>&1; then
  curl -LsSf https://astral.sh/uv/install.sh | sh
fi

specify_version=""
if [[ -f "$init_options_path" ]]; then
  specify_version="$(python - "$init_options_path" <<'PY'
import json
import sys
from pathlib import Path
path = Path(sys.argv[1])
try:
    data = json.loads(path.read_text())
except (OSError, json.JSONDecodeError) as exc:
    raise SystemExit(f"Could not read valid Spec Kit options from {path}: {exc}")
version = data.get("speckit_version")
if version is not None:
    if not isinstance(version, str):
        raise SystemExit(f"speckit_version in {path} must be a string")
    if version:
        print(version)
PY
)"
fi

if [[ -z "$specify_version" ]]; then
  specify_version="1.2.6"
fi

if ! command -v specify >/dev/null 2>&1 || [[ "$(specify --version 2>/dev/null || true)" != *"$specify_version"* ]]; then
  uv tool install --force --reinstall "specify-cli==$specify_version"
fi

specify --version
