#!/usr/bin/env bash
# Installed by RPG Framework > Game Kit > Install AI Guidance; reinstalling replaces this file.
#
# Claude Code SessionStart hook (.claude/settings.json): when a newer framework version exists than the tag in
# Packages/manifest.json, it prints what is new for the agent: each newer version's date, whether it is breaking, the
# requests it answers and a summary, from the framework's release feed (releases.json at the newest tag). It checks once
# a day (cache in Library/RPGFramework), never asks for credentials and never fails the session: on any problem it is
# silent. Turn it off with "env": { "RPG_FRAMEWORK_UPDATE_CHECK": "off" } in .claude/settings.json.

[ "${RPG_FRAMEWORK_UPDATE_CHECK:-on}" = "off" ] && exit 0
root="${CLAUDE_PROJECT_DIR:-$(pwd)}"
manifest="$root/Packages/manifest.json"
[ -f "$manifest" ] || exit 0

# "com.aizekhan.rpg-framework": "https://host/owner/repo.git?path=/Packages/com.aizekhan.rpg-framework#v0.36.0"
value=$(grep -o '"com\.aizekhan\.rpg-framework"[[:space:]]*:[[:space:]]*"[^"]*"' "$manifest" | head -n 1 |
  sed 's/^"com\.aizekhan\.rpg-framework"[[:space:]]*:[[:space:]]*"//; s/"$//')
case "$value" in
  *'#v'[0-9]*) ;;
  *) exit 0 ;; # a registry version (Package Manager shows its updates) or a local copy
esac
current=${value##*#v}
url=${value%%#*}
url=${url%%\?*}
path=""
case "$value" in *'?path='*) path=${value#*\?path=}; path=${path%%#*}; path=${path#/} ;; esac

cache_dir="$root/Library/RPGFramework"
cache="$cache_dir/update-check.txt"
key="$(date +%Y-%m-%d) $current"
if [ -f "$cache" ] && [ "$(head -n 1 "$cache")" = "$key" ]; then
  tail -n +2 "$cache"
  exit 0
fi

tags=$(GIT_TERMINAL_PROMPT=0 GCM_INTERACTIVE=never git -c http.lowSpeedLimit=1000 -c http.lowSpeedTime=8 \
  ls-remote --tags --refs "$url" 'v*' 2>/dev/null) || exit 0
newer=$(printf '%s\n' "$tags" | sed -n 's#.*refs/tags/v\([0-9][0-9]*\.[0-9][0-9]*\.[0-9][0-9]*\)$#\1#p' |
  awk -F. -v cur="$current" 'BEGIN { split(cur, c, ".") }
    $1 + 0 > c[1] + 0 || ($1 + 0 == c[1] + 0 && ($2 + 0 > c[2] + 0 || ($2 + 0 == c[2] + 0 && $3 + 0 > c[3] + 0)))' |
  sort -t. -k1,1n -k2,2n -k3,3n)

report=""
if [ -n "$newer" ]; then
  newest=$(printf '%s\n' "$newer" | tail -n 1)
  feed=""
  changelog="the CHANGELOG.md of v$newest"
  case "$url" in
    https://github.com/*)
      repo=${url#https://github.com/}
      repo=${repo%.git}
      base="https://raw.githubusercontent.com/$repo/v$newest/${path:+$path/}"
      changelog="${base}CHANGELOG.md"
      feed=$(curl -fsSL --max-time 8 "${base}releases.json" 2>/dev/null) || feed=""
      ;;
  esac
  [ -n "${RPG_FRAMEWORK_FEED_FILE:-}" ] && feed=$(cat "$RPG_FRAMEWORK_FEED_FILE") # tests read a local feed
  list=$(printf '%s\n' "$newer" | sed 's/^/v/' | tr '\n' ' ')
  report="RPG Framework update check: this game is on v$current; newer: ${list% }."
  for version in $(printf '%s\n' "$newer" | sort -t. -k1,1nr -k2,2nr -k3,3nr); do
    line=$(printf '%s\n' "$feed" | grep -F "\"version\": \"$version\"" | head -n 1)
    [ -n "$line" ] || continue
    date=$(printf '%s' "$line" | sed -n 's/.*"date": "\([^"]*\)".*/\1/p')
    breaking=$(printf '%s' "$line" | sed -n 's/.*"breaking": \([a-z]*\).*/\1/p')
    refs=$(printf '%s' "$line" | sed -n 's/.*"refs": \[\([^]]*\)\].*/\1/p' | tr -d '"')
    summary=$(printf '%s' "$line" | sed -n 's/.*"summary": "\(.*\)"}.*/\1/p' | sed 's/\\"/"/g')
    case "$breaking" in
      true) breaking="BREAKING (its upgrade steps are in releases.json and the CHANGELOG)" ;;
      false) breaking="not breaking" ;;
      *) breaking="breaking unknown (read the CHANGELOG)" ;;
    esac
    report="$report
- v$version ($date, $breaking${refs:+; answers $refs}): $summary"
  done
  report="$report
Tell the developer at the start of this session. Before updating, read the CHANGELOG entries between v$current and
v$newest ($changelog) and check them against the open gaps in FRAMEWORK-GAPS.md (the ids above are the asking game's own: match a gap by
what it needs, not by its id). Update only when the developer agrees,
or right away when CLAUDE.md gives standing permission; then follow \"After the framework is updated\" in CLAUDE.md."
fi

mkdir -p "$cache_dir" 2>/dev/null && printf '%s\n%s' "$key" "$report" > "$cache" 2>/dev/null
[ -n "$report" ] && printf '%s\n' "$report"
exit 0
