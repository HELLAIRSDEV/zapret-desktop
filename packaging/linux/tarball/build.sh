#!/usr/bin/env bash
# Собирает переносимый tar.gz с Linux-сборкой.
# Использование: build.sh <publish-dir> <version> [out-dir]
set -euo pipefail

PUBLISH_DIR="${1:?publish dir}"
VERSION="${2:?version}"
OUT_DIR="${3:-.}"
ARCH="${ARCH:-x86_64}"

ROOT="$(cd "$(dirname "$0")" && pwd)"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

NAME="ZapretDesktop-linux-${ARCH}"
DEST="$WORK/$NAME"

mkdir -p "$DEST"
cp -a "$PUBLISH_DIR/." "$DEST/"
chmod +x "$DEST/ZapretDesktop" "$DEST/bin/zapret-ctl"
[ -f "$DEST/bin/nfqws" ] && chmod +x "$DEST/bin/nfqws"

cat > "$DEST/README-LINUX.txt" <<'EOF'
Zapret Desktop для Linux
========================

Запуск:
  ./ZapretDesktop

Требования:
  - nftables (правила для NFQUEUE)
  - polkit / pkexec (запрос root-прав при включении обхода)
  - nfqws — уже лежит в папке bin/ рядом с бинарником

При первом включении обхода система попросит пароль администратора:
создаются правила nftables и запускается nfqws.

Настройки, списки и логи хранятся в:
  ~/.local/share/ZapretDesktop      (если папка программы недоступна для записи)
  рядом с бинарником               (если запущено из распакованного архива)
EOF

mkdir -p "$OUT_DIR"
tar czf "$OUT_DIR/${NAME}-v${VERSION}.tar.gz" -C "$WORK" "$NAME"
echo "Готово: $OUT_DIR/${NAME}-v${VERSION}.tar.gz"
