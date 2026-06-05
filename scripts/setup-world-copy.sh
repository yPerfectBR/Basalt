#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
SRC="$ROOT/worlds/world"
DST="$ROOT/worlds/world_copy"
REG="$ROOT/scripts/world_copy.world.json"

if [[ ! -d "$SRC" ]]; then
  echo "Source world not found: $SRC" >&2
  exit 1
fi

if [[ ! -d "$DST" ]]; then
  echo "Copying $SRC -> $DST"
  cp -a "$SRC" "$DST"
fi

cp "$REG" "$DST/world.json"
echo "Wrote $DST/world.json (allowedWorkers: [0, 1])"
