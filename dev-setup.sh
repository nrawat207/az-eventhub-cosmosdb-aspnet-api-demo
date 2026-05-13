#!/usr/bin/env bash
set -euo pipefail

command_exists() {
  command -v "$1" >/dev/null 2>&1
}

wait_for_http() {
  local name="$1"
  local url="$2"
  local curl_args="${3:-}"
  local attempts="${4:-60}"

  printf "Waiting for %s" "$name"
  for _ in $(seq 1 "$attempts"); do
    if curl -sS ${curl_args} "$url" >/dev/null 2>&1; then
      printf "\n%s is ready.\n" "$name"
      return 0
    fi

    printf "."
    sleep 5
  done

  printf "\nTimed out waiting for %s at %s.\n" "$name" "$url" >&2
  return 1
}

for tool in dotnet docker az curl; do
  if ! command_exists "$tool"; then
    echo "Missing prerequisite: $tool" >&2
    exit 1
  fi
done

if ! docker compose version >/dev/null 2>&1; then
  echo "Docker Compose v2 is required. Install or update Docker Desktop." >&2
  exit 1
fi

if [ ! -f .env ]; then
  cp .env.example .env
  echo "Created .env from .env.example."
else
  echo ".env already exists; leaving it unchanged."
fi

docker compose up -d --build

wait_for_http "Azurite Blob service" "http://localhost:10000/devstoreaccount1?comp=list"
wait_for_http "Cosmos DB Emulator" "https://localhost:8081/_explorer/emulator.pem" "-k" 90
wait_for_http "BatchProcessor.Api" "http://localhost:5001/health" "" 60
wait_for_http "ProgressReceiver.Api" "http://localhost:5002/health" "" 60

cat <<'INFO'

Local development stack is running:
  BatchProcessor.Api:  http://localhost:5001
  ProgressReceiver.Api: http://localhost:5002
  Azurite Blob:        http://localhost:10000/devstoreaccount1
  Cosmos DB Emulator:  https://localhost:8081
  Event Hubs Emulator: sb://localhost

Start a batch job:
  curl -X POST http://localhost:5001/api/batch/start \
    -H "Content-Type: application/json" \
    -d '{"jobName":"local-demo","totalItems":10}'

Then copy the returned jobId and check:
  curl http://localhost:5001/api/batch/<jobId>/status
  curl http://localhost:5002/api/jobs
INFO
