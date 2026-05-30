#!/usr/bin/env bash
set -euo pipefail

PACKAGE_PATH="${1:-}"
API_KEY="${2:-github-actions}"
OWNER="${3:-}"
REPOSITORY="${4:-}"
TOKEN="${5:-}"
BRANCH="${6:-}"
ROOT_PATH="${7:-}"
API_BASE_URL="${8:-https://api.github.com}"

if [ -z "$OWNER" ]; then
  echo "Input 'owner' is required"
  exit 1
fi

if [ -z "$REPOSITORY" ]; then
  echo "Input 'repository' is required"
  exit 1
fi

if [ -z "$TOKEN" ]; then
  echo "Input 'token' is required"
  exit 1
fi

URL="http://127.0.0.1:5050"
SOURCE_URL="$URL/v3/index.json"
LOG_FILE="/tmp/bagetter.log"

cleanup() {
  if [ -n "${BAGETTER_PID:-}" ] && kill -0 "$BAGETTER_PID" 2> /dev/null; then
    kill "$BAGETTER_PID" 2> /dev/null || true
    wait "$BAGETTER_PID" 2> /dev/null || true
  fi
}
trap cleanup EXIT

ApiKey="$API_KEY" \
Database__Type="Sqlite" \
Database__ConnectionString="Data Source=/tmp/bagetter.db" \
Search__Type="Database" \
Storage__Type="GitHub" \
Storage__Owner="$OWNER" \
Storage__Repository="$REPOSITORY" \
Storage__Token="$TOKEN" \
Storage__Branch="$BRANCH" \
Storage__RootPath="$ROOT_PATH" \
Storage__ApiBaseUrl="$API_BASE_URL" \
dotnet /app/BaGetter.dll --urls "$URL" > "$LOG_FILE" 2>&1 &

BAGETTER_PID="$!"
echo "Started BaGetter with PID $BAGETTER_PID"

for attempt in $(seq 1 60); do
  if curl -fsS "$SOURCE_URL" > /dev/null 2>&1; then
    echo "BaGetter is ready at $SOURCE_URL"
    break
  fi

  if ! kill -0 "$BAGETTER_PID" 2> /dev/null; then
    echo "BaGetter exited before becoming ready"
    cat "$LOG_FILE"
    exit 1
  fi

  if [ "$attempt" = "60" ]; then
    echo "BaGetter did not become ready in time"
    cat "$LOG_FILE"
    exit 1
  fi

  sleep 1
done

resolve_packages() {
  local path="$1"

  if [ -f "$path" ]; then
    case "$path" in
      *.symbols.nupkg|*.snupkg) ;;
      *.nupkg) printf '%s\0' "$path" ;;
    esac
    return
  fi

  if [ -d "$path" ]; then
    find "$path" -maxdepth 1 -type f -name '*.nupkg' ! -name '*.symbols.nupkg' ! -name '*.snupkg' -print0
    return
  fi

  find . -path "./$path" -type f -name '*.nupkg' ! -name '*.symbols.nupkg' ! -name '*.snupkg' -print0
}

FOUND_PACKAGE=false
while IFS= read -r -d '' PACKAGE_FILE; do
  FOUND_PACKAGE=true
  echo "Pushing $PACKAGE_FILE"
  STATUS_CODE="$(curl \
    --silent \
    --show-error \
    --output /tmp/bagetter-push-response.txt \
    --write-out '%{http_code}' \
    --request PUT \
    --header "X-NuGet-ApiKey: $API_KEY" \
    --header "Content-Type: application/octet-stream" \
    --data-binary "@$PACKAGE_FILE" \
    "$URL/api/v2/package")"

  case "$STATUS_CODE" in
    201)
      echo "Pushed $PACKAGE_FILE"
      ;;
    409)
      echo "Skipped duplicate $PACKAGE_FILE"
      ;;
    *)
      echo "Failed to push $PACKAGE_FILE. HTTP status: $STATUS_CODE"
      cat /tmp/bagetter-push-response.txt
      exit 1
      ;;
  esac
done < <(resolve_packages "$PACKAGE_PATH")

if [ "$FOUND_PACKAGE" = "false" ]; then
  echo "No uploadable .nupkg files were found at '$PACKAGE_PATH'"
  exit 1
fi
