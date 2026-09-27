#!/usr/bin/env bash
# Configures the published Blazor client for production, before the Static Web App deploy (release.yml):
#   - __API_ORIGIN__ in staticwebapp.config.json (CSP connect-src) and appsettings.Production.json (Api:BaseUrl)
#   - __IMPORTMAP_HASH__ in the CSP script-src: the SHA-256 of the inline import map that publish writes into index.html
# Fails if a placeholder is left, so a release never ships an unconfigured CSP.
# Usage: configure-client.sh <published wwwroot folder> <api origin, e.g. https://api.example.com>
set -euo pipefail

wwwroot="${1:?published wwwroot folder required}"
api_origin="${2:?api origin required}"

if [[ ! "$api_origin" =~ ^https://[a-z0-9-]+(\.[a-z0-9-]+)+$ ]]; then
  echo "::error::API origin must be lower-case https://host without port or path, got '$api_origin'"
  exit 1
fi

importmap_hash=$(python3 - "$wwwroot/index.html" <<'PY'
import base64, hashlib, re, sys

html = open(sys.argv[1], encoding="utf-8").read()
inline_scripts = re.findall(r"<script(?![^>]*\bsrc=)([^>]*)>(.*?)</script>", html, re.S)
if len(inline_scripts) != 1 or 'type="importmap"' not in inline_scripts[0][0]:
    sys.exit("index.html must contain exactly one inline script: the import map")
digest = hashlib.sha256(inline_scripts[0][1].encode("utf-8")).digest()
print("'sha256-" + base64.b64encode(digest).decode() + "'")
PY
)

files=("$wwwroot/staticwebapp.config.json" "$wwwroot/appsettings.Production.json")
for file in "${files[@]}"; do
  sed -i.bak -e "s|__API_ORIGIN__|${api_origin}|g" -e "s|__IMPORTMAP_HASH__|${importmap_hash}|g" "$file"
  rm -f "$file.bak" "$file.br" "$file.gz" # stale precompressed copies still hold the placeholders
done

if grep -l -e "__API_ORIGIN__" -e "__IMPORTMAP_HASH__" "${files[@]}"; then
  echo "::error::Client configuration placeholders were not replaced"
  exit 1
fi

echo "Client configured for ${api_origin} (import map ${importmap_hash})"
