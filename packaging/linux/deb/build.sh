#!/usr/bin/env bash
# Собирает .deb из каталога publish.
# Использование: build.sh <publish-dir> <version> [out-dir]
set -euo pipefail

PUBLISH_DIR="${1:?publish dir}"
VERSION="${2:?version}"
OUT_DIR="${3:-.}"
ARCH="${ARCH:-amd64}"

ROOT="$(cd "$(dirname "$0")" && pwd)"
COMMON="$ROOT/../common"
PKG_ROOT="$(mktemp -d)"
trap 'rm -rf "$PKG_ROOT"' EXIT

install -d "$PKG_ROOT/DEBIAN" \
           "$PKG_ROOT/usr/lib/zapret-desktop" \
           "$PKG_ROOT/usr/bin" \
           "$PKG_ROOT/usr/share/applications" \
           "$PKG_ROOT/usr/share/icons/hicolor/256x256/apps" \
           "$PKG_ROOT/usr/share/polkit-1/actions"

cp -a "$PUBLISH_DIR/." "$PKG_ROOT/usr/lib/zapret-desktop/"
chmod +x "$PKG_ROOT/usr/lib/zapret-desktop/ZapretDesktop" \
         "$PKG_ROOT/usr/lib/zapret-desktop/bin/zapret-ctl"
[ -f "$PKG_ROOT/usr/lib/zapret-desktop/bin/nfqws" ] && chmod +x "$PKG_ROOT/usr/lib/zapret-desktop/bin/nfqws"

cat > "$PKG_ROOT/usr/bin/zapret-desktop" <<'EOF'
#!/bin/sh
exec /usr/lib/zapret-desktop/ZapretDesktop "$@"
EOF
chmod 755 "$PKG_ROOT/usr/bin/zapret-desktop"

install -m 644 "$COMMON/zapret-desktop.desktop" "$PKG_ROOT/usr/share/applications/"
install -m 644 "$COMMON/org.zapret.desktop.policy" "$PKG_ROOT/usr/share/polkit-1/actions/"

if [ -f "$ROOT/../../../src/ZapretDesktop.Linux/Assets/zapret.png" ]; then
    install -m 644 "$ROOT/../../../src/ZapretDesktop.Linux/Assets/zapret.png" \
        "$PKG_ROOT/usr/share/icons/hicolor/256x256/apps/zapret-desktop.png"
fi

INSTALLED_SIZE="$(du -sk "$PKG_ROOT" | cut -f1)"

cat > "$PKG_ROOT/DEBIAN/control" <<EOF
Package: zapret-desktop
Version: $VERSION
Section: net
Priority: optional
Architecture: $ARCH
Maintainer: HELLAIRSDEV <hellairss@mail.ru>
Installed-Size: $INSTALLED_SIZE
Depends: nftables, pkexec | policykit-1, libfontconfig1, libfreetype6, libharfbuzz0b, libglib2.0-0, libx11-6, libice6, libsm6, zlib1g
Recommends: libicu72 | libicu74 | libicu76
Homepage: https://github.com/HELLAIRSDEV/zapret-desktop
Description: Zapret Desktop - GUI for DPI bypass (zapret/nfqws)
 Кроссплатформенный графический интерфейс для обхода DPI
 (Discord, YouTube, Telegram) на базе nfqws и nftables.
EOF

cat > "$PKG_ROOT/DEBIAN/postinst" <<'EOF'
#!/bin/sh
set -e
if [ -x /usr/lib/zapret-desktop/bin/zapret-ctl ]; then
    chmod 755 /usr/lib/zapret-desktop/bin/zapret-ctl
fi
if [ -f /usr/lib/zapret-desktop/bin/nfqws ]; then
    chmod 755 /usr/lib/zapret-desktop/bin/nfqws
fi
command -v update-desktop-database >/dev/null 2>&1 && update-desktop-database -q || true
exit 0
EOF
chmod 755 "$PKG_ROOT/DEBIAN/postinst"

mkdir -p "$OUT_DIR"
dpkg-deb --root-owner-group --build "$PKG_ROOT" \
    "$OUT_DIR/zapret-desktop_${VERSION}_${ARCH}.deb"

echo "Готово: $OUT_DIR/zapret-desktop_${VERSION}_${ARCH}.deb"
