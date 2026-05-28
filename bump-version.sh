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

# Update appsettings.json
sed -i '' "s/\"AppVersion\": \"$CURRENT\"/\"AppVersion\": \"$NEW\"/" appsettings.json
# Update .csproj
sed -i '' "s/<Version>$CURRENT<\/Version>/<Version>$NEW<\/Version>/" NordicBeesERP.csproj

echo "Version bumped: $CURRENT -> $NEW"
git add appsettings.json NordicBeesERP.csproj bump-version.sh
git commit -m "chore: bump version to $NEW"
git tag -a "v$NEW" -m "v$NEW"
git push && git push origin "v$NEW"