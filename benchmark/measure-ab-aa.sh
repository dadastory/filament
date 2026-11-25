#!/system/bin/sh
set -eu
cd "$1"
assets="$2"
export LLM4AD_FSR_TIMINGS_ONLY=1
mkdir timing
capture() {
 scene="$1"
 variant="$2"
 log="$3"
 if [ "$variant" = candidate ]; then
  set -- ./candidate.filamat
 else
  set --
 fi
 "$assets/runner" "$assets/$scene.glb" "$assets/environment.ktx" . "$scene" "$@" > "$log" 2>&1
 grep -qx "EASU_PACKAGE $variant" "$log"
}
get_temperature() {
 dumpsys battery | grep -E 'temperature|status' || true
}
get_temperature > timing/start-thermal.txt
for block in 1 2 3 4; do
 if [ "$((block % 2))" = 1 ]; then
  set -- DamagedHelmet Fox
 else
  set -- Fox DamagedHelmet
 fi
 for scene in "$@"; do
  prefix="timing/$scene-$block"
  capture "$scene" stock "$prefix-ab-stock.txt"
  capture "$scene" candidate "$prefix-ab-candidate.txt"
  capture "$scene" stock "$prefix-aa-first.txt"
  capture "$scene" stock "$prefix-aa-second.txt"
  capture "$scene" candidate "$prefix-ba-candidate.txt"
  capture "$scene" stock "$prefix-ba-stock.txt"
 done
 get_temperature > "timing/block-$block-thermal.txt"
done
tar -cf timings.tar timing
