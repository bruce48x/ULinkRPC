#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")/../.." && pwd)"
WORK_DIR="$ROOT_DIR/.tmp/starter-godot-daily"
GENERATED_ROOT="$WORK_DIR/generated"
PROJECT_NAME="StarterGodotWsMemoryPack"
PROJECT_DIR="$GENERATED_ROOT/$PROJECT_NAME"
CLIENT_DIR="$PROJECT_DIR/Client"
SERVER_PROJECT="$PROJECT_DIR/Server/Server/Server.csproj"
CLIENT_PROJECT="$CLIENT_DIR/Client.csproj"
LOG_DIR="$WORK_DIR/logs"
SERVER_LOG="$LOG_DIR/server.log"
CLIENT_LOG="$LOG_DIR/client.log"
LOCAL_FEED="$ROOT_DIR/artifacts/ci-nuget"
CI_NUGET_CONFIG="$WORK_DIR/NuGet.config"

if [[ -z "${GODOT_BIN:-}" || -z "${GODOT_NUPKGS:-}" ]]; then
  echo "GODOT_BIN and GODOT_NUPKGS must be set." >&2
  exit 1
fi

rm -rf "$WORK_DIR" "$LOCAL_FEED"
mkdir -p "$GENERATED_ROOT" "$LOG_DIR" "$LOCAL_FEED"

cleanup() {
  if [[ -n "${SERVER_PID:-}" ]] && kill -0 "$SERVER_PID" 2>/dev/null; then
    kill "$SERVER_PID" 2>/dev/null || true
    wait "$SERVER_PID" 2>/dev/null || true
  fi
}

trap cleanup EXIT

pack_local_package() {
  local project_path="$1"
  dotnet pack "$project_path" -c Release -o "$LOCAL_FEED" --nologo
}

print_logs() {
  if [[ -f "$SERVER_LOG" ]]; then
    echo "===== server.log =====" >&2
    cat "$SERVER_LOG" >&2
  fi

  if [[ -f "$CLIENT_LOG" ]]; then
    echo "===== client.log =====" >&2
    cat "$CLIENT_LOG" >&2
  fi
}

wait_for_port() {
  local host="$1"
  local port="$2"
  local attempts="${3:-60}"

  for ((i = 0; i < attempts; i++)); do
    if exec 3<>"/dev/tcp/$host/$port" 2>/dev/null; then
      exec 3>&-
      exec 3<&-
      return 0
    fi

    if [[ -n "${SERVER_PID:-}" ]] && ! kill -0 "$SERVER_PID" 2>/dev/null; then
      echo "Server process exited before port $port became ready." >&2
      return 1
    fi

    sleep 1
  done

  echo "Timed out waiting for $host:$port." >&2
  return 1
}

cat > "$CI_NUGET_CONFIG" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="local" value="$LOCAL_FEED" />
    <add key="godot-local" value="$GODOT_NUPKGS" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
  </packageSources>
</configuration>
EOF

echo "Packing local packages into $LOCAL_FEED"
pack_local_package "$ROOT_DIR/src/ULinkRPC.Core/ULinkRPC.Core.csproj"
pack_local_package "$ROOT_DIR/src/ULinkRPC.Client/ULinkRPC.Client.csproj"
pack_local_package "$ROOT_DIR/src/ULinkRPC.Server/ULinkRPC.Server.csproj"
pack_local_package "$ROOT_DIR/src/ULinkRPC.Transport.WebSocket/ULinkRPC.Transport.WebSocket.csproj"
pack_local_package "$ROOT_DIR/src/ULinkRPC.Serializer.MemoryPack/ULinkRPC.Serializer.MemoryPack.csproj"

export ULINKRPC_GODOT_NUPKGS="$GODOT_NUPKGS"
export ULINKRPC_STARTER_LOCAL_CODEGEN_PROJECT="$ROOT_DIR/src/ULinkRPC.CodeGen/ULinkRPC.CodeGen.csproj"

echo "Generating starter project at $PROJECT_DIR"
dotnet run --project "$ROOT_DIR/src/ULinkRPC.Starter/ULinkRPC.Starter.csproj" -- \
  --name "$PROJECT_NAME" \
  --output "$GENERATED_ROOT" \
  --client-engine godot \
  --transport websocket \
  --serializer memorypack

echo "Restoring and building generated server"
dotnet restore "$SERVER_PROJECT" --configfile "$CI_NUGET_CONFIG"
dotnet build "$SERVER_PROJECT" -c Release --no-restore

echo "Restoring and building generated Godot client"
dotnet restore "$CLIENT_PROJECT" --configfile "$CI_NUGET_CONFIG"
dotnet build "$CLIENT_PROJECT" -c Release --no-restore

echo "Starting generated server"
dotnet run --project "$SERVER_PROJECT" -c Release --no-build >"$SERVER_LOG" 2>&1 &
SERVER_PID=$!

if ! wait_for_port 127.0.0.1 20000; then
  print_logs
  exit 1
fi

echo "Running generated Godot client headless"
timeout 90s "$GODOT_BIN" \
  --headless \
  --path "$CLIENT_DIR" \
  --scene "res://Main.tscn" \
  --quit-after 300 \
  --log-file "$CLIENT_LOG" \
  --verbose \
  --no-header

if grep -Fq "Connect failed:" "$CLIENT_LOG"; then
  echo "Godot client reported a connection failure." >&2
  print_logs
  exit 1
fi

if ! grep -Fq "Ping ok:" "$CLIENT_LOG"; then
  echo "Did not find successful ping log in Godot output." >&2
  print_logs
  exit 1
fi

echo "Starter Godot websocket + memorypack verification passed."
