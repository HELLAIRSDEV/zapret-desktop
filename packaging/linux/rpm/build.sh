#!/usr/bin/env bash
# Собирает .rpm из каталога publish.
# Использование: build.sh <publish-dir> <version> [out-dir]
set -euo pipefail

PUBLISH_DIR="${1:?publish dir}"
VERSION="${2:?version}"
OUT_DIR="${3:-.}"

ROOT="$(cd "$(dirname "$0")" && pwd)"
COMMON="$ROOT/../common"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

STAGE="$WORK/stage"
install -d "$STAGE/usr/lib/zapret-desktop" \
           "$STAGE/usr/bin" \
           "$STAGE/usr/share/applications" \
           "$STAGE/usr/share/icons/hicolor/256x256/apps" \
           "$STAGE/usr/share/polkit-1/actions"

cp -a "$PUBLISH_DIR/." "$STAGE/usr/lib/zapret-desktop/"
chmod +x "$STAGE/usr/lib/zapret-desktop/ZapretDesktop" \
         "$STAGE/usr/lib/zapret-desktop/bin/zapret-ctl"
[ -f "$STAGE/usr/lib/zapret-desktop/bin/nfqws" ] && chmod +x "$STAGE/usr/lib/zapret-desktop/bin/nfqws"

cat > "$STAGE/usr/bin/zapret-desktop" <<'EOF'
#!/bin/sh
exec /usr/lib/zapret-desktop/ZapretDesktop "$@"
EOF
chmod 755 "$STAGE/usr/bin/zapret-desktop"

install -m 644 "$COMMON/zapret-desktop.desktop" "$STAGE/usr/share/applications/"
install -m 644 "$COMMON/org.zapret.desktop.policy" "$STAGE/usr/share/polkit-1/actions/"

if [ -f "$ROOT/../../../src/ZapretDesktop.Linux/Assets/zapret.png" ]; then
    install -m 644 "$ROOT/../../../src/ZapretDesktop.Linux/Assets/zapret.png" \
        "$STAGE/usr/share/icons/hicolor/256x256/apps/zapret-desktop.png"
fi

TOPDIR="$WORK/rpmbuild"
mkdir -p "$TOPDIR"/{BUILD,BUILDROOT,RPMS,SOURCES,SPECS,SRPMS}

tar czf "$TOPDIR/SOURCES/zapret-desktop-tree.tar.gz" -C "$STAGE" .
sed "s/@VERSION@/$VERSION/g" "$ROOT/zapret-desktop.spec.in" > "$TOPDIR/SPECS/zapret-desktop.spec"

rpmbuild -bb "$TOPDIR/SPECS/zapret-desktop.spec" \
    --define "_topdir $TOPDIR" \
    --define "_binary_payload w9.gzdio" >/dev/null

mkdir -p "$OUT_DIR"
find "$TOPDIR/RPMS" -name '*.rpm' -exec cp {} "$OUT_DIR/" \;
ls -1 "$OUT_DIR"/*.rpm
