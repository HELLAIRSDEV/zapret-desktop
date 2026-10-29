#!/usr/bin/env bash
# Собирает .pkg.tar.zst (Arch) из каталога publish.
# Использование: build.sh <publish-dir> <version> [out-dir]
# Требует pacman/makepkg (контейнер archlinux).
set -euo pipefail

PUBLISH_DIR="${1:?publish dir}"
VERSION="${2:?version}"
OUT_DIR="${3:-.}"

ROOT="$(cd "$(dirname "$0")" && pwd)"
COMMON="$ROOT/../common"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

PKGNAME="zapret-desktop"

install -d "$WORK/stage/usr/lib/$PKGNAME" \
           "$WORK/stage/usr/bin" \
           "$WORK/stage/usr/share/applications" \
           "$WORK/stage/usr/share/icons/hicolor/256x256/apps" \
           "$WORK/stage/usr/share/polkit-1/actions"

cp -a "$PUBLISH_DIR/." "$WORK/stage/usr/lib/$PKGNAME/"
chmod +x "$WORK/stage/usr/lib/$PKGNAME/ZapretDesktop" \
         "$WORK/stage/usr/lib/$PKGNAME/bin/zapret-ctl"
[ -f "$WORK/stage/usr/lib/$PKGNAME/bin/nfqws" ] && chmod +x "$WORK/stage/usr/lib/$PKGNAME/bin/nfqws"

cat > "$WORK/stage/usr/bin/zapret-desktop" <<'EOF'
#!/bin/sh
exec /usr/lib/zapret-desktop/ZapretDesktop "$@"
EOF
chmod 755 "$WORK/stage/usr/bin/zapret-desktop"

install -m 644 "$COMMON/zapret-desktop.desktop" "$WORK/stage/usr/share/applications/"
install -m 644 "$COMMON/org.zapret.desktop.policy" "$WORK/stage/usr/share/polkit-1/actions/"

if [ -f "$ROOT/../../src/ZapretDesktop.Linux/Assets/zapret.png" ]; then
    install -m 644 "$ROOT/../../src/ZapretDesktop.Linux/Assets/zapret.png" \
        "$WORK/stage/usr/share/icons/hicolor/256x256/apps/zapret-desktop.png"
fi

PKGVER="$(echo "$VERSION" | tr '-' '_')"

# Собираем .PKGINFO вручную и упаковываем через bsdtar — без makepkg/fakeroot.
(
  cd "$WORK/stage"
  SIZE="$(du -sk usr | cut -f1)"

  cat > .PKGINFO <<EOF
pkgname = $PKGNAME
pkgver = $PKGVER-1
pkgdesc = GUI for DPI bypass (zapret / nfqws)
url = https://github.com/HELLAIRSDEV/zapret-desktop
builddate = $(date +%s)
packager = HELLAIRSDEV <hellairss@mail.ru>
size = $((SIZE * 1024))
arch = x86_64
license = MIT
depend = nftables
depend = polkit
depend = fontconfig
depend = freetype2
depend = harfbuzz
depend = glib2
depend = libx11
depend = libice
depend = libsm
depend = zlib
EOF

  chmod 755 .PKGINFO

  bsdtar -czf .MTREE --format=mtree \
      --options='!all,use-set,type,uid,gid,mode,time,size,md5,sha256,link' \
      .PKGINFO usr

  OUT="$OUT_DIR/${PKGNAME}-${PKGVER}-1-x86_64.pkg.tar.zst"
  bsdtar --use-compress-program zstd -cf "$OUT" .PKGINFO .MTREE usr
  echo "Готово: $OUT"
)
