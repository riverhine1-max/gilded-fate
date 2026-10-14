#!/usr/bin/env bash
# Static checks for the Chronicle QA build. Runs in the cheap "plan" job, before any Unity build starts, and locally:
#   bash ci/verify-qa-config.sh
# It proves the QA symbol cannot reach a normal build and that every QA file and call site is compiled out without it.
set -uo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")/.."
FAIL=0
fail() { echo "::error::QA config: $*"; FAIL=1; }
ok() { echo "ok   $*"; }
SYMBOL=GILDED_CHRONICLE_QA

# 1. The symbol is never stored in the project, so no build picks it up by default.
if grep -q "$SYMBOL" ProjectSettings/ProjectSettings.asset; then fail "ProjectSettings.asset defines $SYMBOL, which would put the QA panel in every build."; else ok "ProjectSettings does not define $SYMBOL"; fi

# 2. Only the QA build method passes it.
BUILD=ci/GildedFateCiBuild.cs
qa_line=$(grep -n "public static void BuildWindowsQA" "$BUILD" | head -1 | cut -d: -f2-)
if [ -z "$qa_line" ]; then fail "$BUILD has no BuildWindowsQA method."; elif ! grep -q "$SYMBOL" <<<"$qa_line"; then fail "BuildWindowsQA does not pass $SYMBOL."; else ok "BuildWindowsQA passes $SYMBOL"; fi
for m in BuildWindows BuildLinux BuildWebGL; do
  line=$(grep -n "public static void $m()" "$BUILD" | head -1 | cut -d: -f2-)
  if [ -z "$line" ]; then fail "$BUILD lost $m."; elif grep -q "$SYMBOL\|QA" <<<"$line"; then fail "$m must stay a normal build, but mentions QA."; else ok "$m is untouched by QA"; fi
done
if [ "$(grep -c "\"$SYMBOL\"" "$BUILD")" != "1" ]; then fail "$BUILD should pass $SYMBOL from exactly one place."; fi

# 3. The workflow builds the QA zip separately and leaves the normal release alone.
WF=.github/workflows/build-game.yml
grep -q "GildedFate-Windows-QA.zip" "$WF" && ok "workflow produces GildedFate-Windows-QA.zip" || fail "workflow does not produce GildedFate-Windows-QA.zip."
grep -q "GildedFate-Windows.zip" "$WF" && ok "normal GildedFate-Windows.zip is still produced" || fail "the normal GildedFate-Windows.zip step is gone."
grep -q "BuildWindowsQA" "$WF" && ok "workflow runs BuildWindowsQA" || fail "workflow never runs BuildWindowsQA."
if grep -E 'case "\$targets" in .*(all|windows\+webgl).*\) *items' "$WF" | grep -q "windows-qa\|BuildWindowsQA"; then fail "the QA build must be dispatch-only; it is part of an automatic target list."; else ok "QA build is not part of 'all' or the push builds"; fi
qa_step=$(awk '/- name: Publish Chronicle QA build/{on=1; next} on && /^[[:space:]]*#/{next} on && /^      - (name|uses):/{exit} on{print}' "$WF")
win_step=$(awk '/- name: Publish Windows playtest/{on=1; next} on && /^[[:space:]]*#/{next} on && /^      - (name|uses):/{exit} on{print}' "$WF")
if [ -z "$qa_step" ]; then fail "workflow has no 'Publish Chronicle QA build' step."
elif grep -Eq 'gh release [a-z]+ +playtest' <<<"$qa_step"; then fail "the QA step must not touch the 'playtest' release."
elif ! grep -Eq 'gh release upload +chronicle-qa +GildedFate-Windows-QA.zip' <<<"$qa_step"; then fail "the QA step must upload GildedFate-Windows-QA.zip to the 'chronicle-qa' release."
else ok "QA zip goes to its own 'chronicle-qa' release, never 'playtest'"; fi
if grep -q "QA" <<<"$win_step"; then fail "the normal Windows publish step mentions QA."; else ok "the normal Windows publish step is unchanged by QA"; fi

# 4. Every QA source file is compiled out of a normal build.
GUARD='#if UNITY_EDITOR || GILDED_CHRONICLE_QA'
for f in Assets/Scripts/Chronicle/ChronicleQa.cs Assets/Scripts/Chronicle/ChronicleQaPerf.cs Assets/Scripts/Saving/ChronicleQaMode.cs Assets/Scripts/UI/GildedChronicleQa.cs Assets/Scripts/UI/GildedChronicleQaPerf.cs; do
  [ -f "$f" ] || { fail "$f is missing."; continue; }
  first=$(grep -v '^\s*$' "$f" | head -1 | tr -d '\r\357\273\277'); last=$(grep -v '^\s*$' "$f" | tail -1 | tr -d '\r')
  case "$first" in "#if UNITY_EDITOR || GILDED_CHRONICLE_QA"*) ;; *) fail "$f must open with a '$GUARD' guard (found: $first)";; esac
  [ "$last" = "#endif" ] && ok "$f is fully guarded" || fail "$f must close its guard with #endif on the last line."
done

# 5. Every use of QA code elsewhere in the game sits inside such a guard.
leaks=$(for f in $(grep -rl "ChronicleQa" Assets/Scripts --include=*.cs | grep -v -e Chronicle/ChronicleQa.cs -e Chronicle/ChronicleQaPerf.cs -e Saving/ChronicleQaMode.cs -e UI/GildedChronicleQa.cs -e UI/GildedChronicleQaPerf.cs); do
  awk -v file="$f" '
    function inqa(  i) { for (i = 1; i <= depth; i++) if (q[i]) return 1; return 0 }
    /^[[:space:]]*#if/    { depth++; q[depth] = ($0 ~ /GILDED_CHRONICLE_QA/); next }
    /^[[:space:]]*#elif/  { q[depth] = ($0 ~ /GILDED_CHRONICLE_QA/); next }
    /^[[:space:]]*#else/  { q[depth] = 0; next }
    /^[[:space:]]*#endif/ { depth--; next }
    { line = $0; sub(/\/\/.*/, "", line) }
    line ~ /ChronicleQa/ && !inqa() { printf "%s:%d: %s\n", file, NR, $0 }
  ' "$f"
done)
if [ -n "$leaks" ]; then fail "QA code is referenced outside a $SYMBOL guard:"; echo "$leaks"; else ok "every reference to QA code outside the QA files is guarded"; fi

[ "$FAIL" = 0 ] && echo "QA configuration verified." || { echo "QA configuration check FAILED."; exit 1; }
