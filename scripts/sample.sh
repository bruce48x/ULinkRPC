#!/usr/bin/env bash

set -euo pipefail

sample="RpcCall.MemoryPack"
configuration="Debug"
verbosity="minimal"
gen_mode="All"
port=""
run_sample=false
skip_gen=false
skip_build=false
no_restore=false
no_build_tool=false
allow_parallel=false
disable_build_server=false
ignore_failed_sources=false
clean_generated=false
server_args=()

usage() {
  cat <<'EOF'
Usage: ./scripts/sample.sh [options] [-- server-args...]

Options:
  --sample <RpcCall.MemoryPack|RpcCall.Json|RpcCall.Kcp>
  --configuration <Debug|Release>
  --verbosity <quiet|minimal|normal|detailed|diagnostic>
  --gen-mode <Unity|Server|All>
  --port <number>
  --run
  --skip-gen
  --skip-build
  --no-restore
  --no-build-tool
  --allow-parallel
  --disable-build-server
  --ignore-failed-sources
  --clean-generated
  -h, --help
EOF
}

require_value() {
  local option="$1"
  local value="${2:-}"
  if [[ -z "$value" ]]; then
    echo "Missing value for $option" >&2
    exit 1
  fi
}

while (($# > 0)); do
  case "$1" in
    --sample)
      require_value "$1" "${2:-}"
      sample="$2"
      shift 2
      ;;
    --configuration)
      require_value "$1" "${2:-}"
      configuration="$2"
      shift 2
      ;;
    --verbosity)
      require_value "$1" "${2:-}"
      verbosity="$2"
      shift 2
      ;;
    --gen-mode)
      require_value "$1" "${2:-}"
      gen_mode="$2"
      shift 2
      ;;
    --port)
      require_value "$1" "${2:-}"
      port="$2"
      shift 2
      ;;
    --run)
      run_sample=true
      shift
      ;;
    --skip-gen)
      skip_gen=true
      shift
      ;;
    --skip-build)
      skip_build=true
      shift
      ;;
    --no-restore)
      no_restore=true
      shift
      ;;
    --no-build-tool)
      no_build_tool=true
      shift
      ;;
    --allow-parallel)
      allow_parallel=true
      shift
      ;;
    --disable-build-server)
      disable_build_server=true
      shift
      ;;
    --ignore-failed-sources)
      ignore_failed_sources=true
      shift
      ;;
    --clean-generated)
      clean_generated=true
      shift
      ;;
    --help|-h)
      usage
      exit 0
      ;;
    --)
      shift
      server_args=("$@")
      break
      ;;
    *)
      echo "Unknown argument: $1" >&2
      usage >&2
      exit 1
      ;;
  esac
done

case "$sample" in
  RpcCall.MemoryPack)
    project_rel="samples/RpcCall.MemoryPack/RpcCall.MemoryPack.Server/RpcCall.MemoryPack.Server/RpcCall.MemoryPack.Server.csproj"
    assembly_name="RpcCall.MemoryPack.Server"
    contracts_rel="samples/RpcCall.MemoryPack/RpcCall.MemoryPack.Unity/Packages/com.samples.contracts"
    unity_output_rel="samples/RpcCall.MemoryPack/RpcCall.MemoryPack.Unity/Assets/Scripts/Rpc/Generated"
    server_output_rel="samples/RpcCall.MemoryPack/RpcCall.MemoryPack.Server/RpcCall.MemoryPack.Server/Generated"
    ;;
  RpcCall.Json)
    project_rel="samples/RpcCall.Json/RpcCall.Json.Server/RpcCall.Json.Server/RpcCall.Json.Server.csproj"
    assembly_name="RpcCall.Json.Server"
    contracts_rel="samples/RpcCall.Json/RpcCall.Json.Unity/Packages/com.samples.contracts"
    unity_output_rel="samples/RpcCall.Json/RpcCall.Json.Unity/Assets/Scripts/Rpc/Generated"
    server_output_rel="samples/RpcCall.Json/RpcCall.Json.Server/RpcCall.Json.Server/Generated"
    ;;
  RpcCall.Kcp)
    project_rel="samples/RpcCall.Kcp/RpcCall.Kcp.Server/RpcCall.Kcp.Server/RpcCall.Kcp.Server.csproj"
    assembly_name="RpcCall.Kcp.Server"
    contracts_rel="samples/RpcCall.Kcp/RpcCall.Kcp.Unity/Packages/com.samples.contracts"
    unity_output_rel="samples/RpcCall.Kcp/RpcCall.Kcp.Unity/Assets/Scripts/Rpc/Generated"
    server_output_rel="samples/RpcCall.Kcp/RpcCall.Kcp.Server/RpcCall.Kcp.Server/Generated"
    ;;
  *)
    echo "Unsupported sample: $sample" >&2
    exit 1
    ;;
esac

case "$configuration" in
  Debug|Release) ;;
  *)
    echo "Unsupported configuration: $configuration" >&2
    exit 1
    ;;
esac

case "$verbosity" in
  quiet|minimal|normal|detailed|diagnostic) ;;
  *)
    echo "Unsupported verbosity: $verbosity" >&2
    exit 1
    ;;
esac

case "$gen_mode" in
  Unity|Server|All) ;;
  *)
    echo "Unsupported gen mode: $gen_mode" >&2
    exit 1
    ;;
esac

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd "$script_dir/.." && pwd)"
tool_project="$repo_root/src/ULinkRPC.CodeGen/ULinkRPC.CodeGen.csproj"
project_path="$repo_root/$project_rel"
project_dir="$(dirname "$project_path")"
contracts_path="$repo_root/$contracts_rel"
unity_output_path="$repo_root/$unity_output_rel"
server_output_path="$repo_root/$server_output_rel"
target_dll_path="$project_dir/bin/$configuration/net10.0/$assembly_name.dll"

for path in "$project_path" "$contracts_path"; do
  if [[ ! -e "$path" ]]; then
    echo "Required path not found: $path" >&2
    exit 1
  fi
done

export DOTNET_CLI_HOME="$repo_root/.dotnet"
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export NUGET_PACKAGES="$repo_root/.nuget/packages"
export MSBUILDDISABLENODEREUSE=1

mkdir -p "$DOTNET_CLI_HOME" "$NUGET_PACKAGES"

run_dotnet() {
  local command="$1"
  shift
  echo "==> dotnet $command $*"
  dotnet "$command" "$@"
}

append_msbuild_args() {
  if $allow_parallel; then
    printf '%s\n' "-m"
  else
    printf '%s\n' "-m:1" "/nr:false"
  fi
}

remove_generated_files() {
  local path="$1"
  [[ -d "$path" ]] || return 0
  find "$path" -maxdepth 1 -type f -delete
}

stop_sample_processes() {
  local terms=("$@")
  while IFS= read -r line; do
    local pid="${line%% *}"
    local cmd="${line#* }"
    [[ -n "$pid" ]] || continue
    [[ "$pid" == "$$" ]] && continue
    for term in "${terms[@]}"; do
      [[ -n "$term" ]] || continue
      if [[ "$cmd" == *"$term"* ]]; then
        if kill "$pid" 2>/dev/null; then
          echo "==> stopped existing process $pid for $sample"
        fi
        break
      fi
    done
  done < <(ps -ax -o pid= -o command=)
}

if ! $skip_gen; then
  if ! $no_build_tool; then
    build_tool_args=("$tool_project" "-c" "$configuration" "-v" "$verbosity")
    while IFS= read -r arg; do
      build_tool_args+=("$arg")
    done < <(append_msbuild_args)
    if $disable_build_server; then
      build_tool_args+=("--disable-build-servers")
    fi
    if $no_restore; then
      build_tool_args+=("--no-restore")
    fi
    run_dotnet build "${build_tool_args[@]}"
  fi

  if $clean_generated; then
    if [[ "$gen_mode" == "Unity" || "$gen_mode" == "All" ]]; then
      remove_generated_files "$unity_output_path"
    fi
    if [[ "$gen_mode" == "Server" || "$gen_mode" == "All" ]]; then
      remove_generated_files "$server_output_path"
    fi
  fi

  tool_run_base_args=(run --project "$tool_project" -c "$configuration")
  if $no_build_tool; then
    tool_run_base_args+=("--no-build")
  fi
  if $no_restore; then
    tool_run_base_args+=("--no-restore")
  fi
  tool_run_base_args+=("--")

  if [[ "$gen_mode" == "Unity" || "$gen_mode" == "All" ]]; then
    unity_args=("${tool_run_base_args[@]}" --contracts "$contracts_path" --mode unity --output "$unity_output_path")
    echo "==> dotnet ${unity_args[*]}"
    dotnet "${unity_args[@]}"
  fi

  if [[ "$gen_mode" == "Server" || "$gen_mode" == "All" ]]; then
    server_gen_args=("${tool_run_base_args[@]}" --contracts "$contracts_path" --mode server --server-output "$server_output_path")
    echo "==> dotnet ${server_gen_args[*]}"
    dotnet "${server_gen_args[@]}"
  fi
fi

if ! $skip_build; then
  stop_sample_processes "$project_path" "$target_dll_path" "$assembly_name.dll" "$assembly_name.exe"

  build_args=("$project_path" "-c" "$configuration" "-v" "$verbosity")
  while IFS= read -r arg; do
    build_args+=("$arg")
  done < <(append_msbuild_args)
  if $disable_build_server; then
    build_args+=("--disable-build-servers")
  fi
  if $no_restore; then
    build_args+=("--no-restore")
  else
    if ! $allow_parallel; then
      build_args+=("/p:RestoreDisableParallel=true")
    fi
    if $ignore_failed_sources; then
      build_args+=("--ignore-failed-sources")
    fi
  fi

  run_dotnet build "${build_args[@]}"
fi

if $run_sample; then
  if $skip_build; then
    stop_sample_processes "$project_path" "$target_dll_path" "$assembly_name.dll" "$assembly_name.exe"
  fi

  if [[ ! -f "$target_dll_path" ]]; then
    echo "Built server assembly not found: $target_dll_path" >&2
    exit 1
  fi

  run_args=("$target_dll_path")
  if [[ -n "$port" ]]; then
    run_args+=("$port")
  fi
  if ((${#server_args[@]} > 0)); then
    run_args+=("${server_args[@]}")
  fi

  echo "==> dotnet ${run_args[*]}"
  exec dotnet "${run_args[@]}"
fi
