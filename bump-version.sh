#!/bin/bash
TYPE=${1:-patch}  # patch, minor, or major

# Read current version from appsettings.json
CURRENT=$(grep '"AppVersion"' appsettings.json | grep -o '[0-9]*\.[0-9]*\.[0-9]*')
IFS='.' read -r MAJOR MINOR PATCH <<< "$CURRENT"

case $TYPE in
  major) MAJOR=$((MAJOR+1)); MINOR=0; PATCH=0 ;;
  minor) MINOR=$((MINOR+1)); PATCH=0 ;;
  patch) PATCH=$((PATCH+1)) ;;
esac

NEW="$MAJOR.$MINOR.$PATCH"

# SAFETY CHECK: refuse to run if there are uncommitted/untracked changes
# to anything OTHER than the version files this script itself manages.
# This exists because a real, recurring incident happened multiple times:
# an agent would run this script while the actual task's code changes
# were still uncommitted (or the commit step was skipped/failed), and
# this script would happily commit+tag+push ONLY the version bump,
# silently leaving the real work stranded as untracked files on disk --
# with no error, no warning, and a misleading git history that looks
# like a clean version-only release. This check makes that failure mode
# mechanically impossible instead of relying on any agent or prompt
# instruction to remember to check first.
DIRTY=$(git status --porcelain | grep -v -E "appsettings\.json|NordicBeesERP\.csproj|bump-version\.sh")
if [ -n "$DIRTY" ]; then
  echo "ERROR: bump-version.sh refused to run." >&2
  echo "There are uncommitted/untracked changes to files other than the version files this script manages:" >&2
  echo "$DIRTY" >&2
  echo "" >&2
  echo "Commit your actual code changes FIRST, then run bump-version.sh." >&2
  exit 1
fi

# GATE 1: refuse to release if the project does not build.
echo "Running dotnet build (gate 1/2)..."
if ! dotnet build --nologo -v quiet > /tmp/bump-version-build.log 2>&1; then
  echo "ERROR: bump-version.sh refused to run -- build FAILED." >&2
  echo "See /tmp/bump-version-build.log for details:" >&2
  tail -n 40 /tmp/bump-version-build.log >&2
  exit 1
fi

# GATE 1.5: run the test suite. Skips gracefully (does not block) if the
# test DB env var is unset, since not every dev machine has
# nordic_bees_erp_test configured -- but if TEST_DB_CONNECTION IS set,
# a failing test blocks the release just like a failing build.
if [ -n "$TEST_DB_CONNECTION" ]; then
  echo "Running dotnet test (gate 1.5)..."
  if ! dotnet test --nologo -v quiet > /tmp/bump-version-test.log 2>&1; then
    echo "ERROR: bump-version.sh refused to run -- tests FAILED." >&2
    echo "See /tmp/bump-version-test.log for details:" >&2
    tail -n 40 /tmp/bump-version-test.log >&2
    exit 1
  fi
else
  echo "Skipping dotnet test (gate 1.5) -- TEST_DB_CONNECTION not set on this machine."
fi

# GATE 2: refuse to release if any staged/modified .cs file matches the
# #1 recurring anti-pattern (see .clinerules/nordicbees-standards.md,
# "EF CORE UPDATE PATTERN" section): FindAsync() + SaveChangesAsync()
# in the same file (detached-entity write that silently persists 0 rows
# under global NoTracking). This is a known, previously-shipped bug
# class -- mechanical check instead of trusting any agent to remember.
echo "Checking for FindAsync+SaveChangesAsync anti-pattern (gate 2/2)..."
LAST_TAG=$(git describe --tags --abbrev=0 2>/dev/null)
if [ -n "$LAST_TAG" ]; then
  CHANGED_CS=$(git diff --name-only "$LAST_TAG" HEAD -- "*.cs")
else
  # No tags yet -- fall back to the most recent commit only.
  CHANGED_CS=$(git diff --name-only HEAD~1 HEAD -- "*.cs" 2>/dev/null)
fi
BAD_FILES=""
for f in $CHANGED_CS; do
  if [ -f "$f" ] && grep -q "FindAsync(" "$f" && grep -q "SaveChangesAsync()" "$f"; then
    BAD_FILES="$BAD_FILES$f
"
  fi
done
if [ -n "$BAD_FILES" ]; then
  echo "ERROR: bump-version.sh refused to run." >&2
  echo "The following changed file(s) contain both FindAsync( and SaveChangesAsync() -- this is the known anti-pattern from .clinerules/nordicbees-standards.md (detached entity, silent 0-row write under global NoTracking):" >&2
  echo -e "$BAD_FILES" >&2
  echo "Fix with ExecuteSqlRawAsync per .clinerules/nordicbees-standards.md, or if this is a false positive (e.g. genuine tracked-entity flow), review manually before bumping version." >&2
  exit 1
fi

# Update appsettings.json
sed -i '' "s/\"AppVersion\": \"$CURRENT\"/\"AppVersion\": \"$NEW\"/" appsettings.json
# Update .csproj
sed -i '' "s/<Version>$CURRENT<\/Version>/<Version>$NEW<\/Version>/" NordicBeesERP.csproj

echo "Version bumped: $CURRENT -> $NEW"
git add appsettings.json NordicBeesERP.csproj bump-version.sh
git commit -m "chore: bump version to $NEW"
if ! git tag -a "v$NEW" -m "v$NEW"; then
  echo "ERROR: git tag failed (tag v$NEW likely already exists on a different commit). Refusing to push. Resolve the version collision manually." >&2
  exit 1
fi
git push
git push origin "v$NEW"