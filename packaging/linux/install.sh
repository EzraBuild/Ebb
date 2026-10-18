#!/bin/sh
# Installs Ebb just for you, no root needed: the app goes to ~/.local/share/ebb, a launcher to
# ~/.local/bin/ebb, and a menu entry with its icon. Run "./install.sh --uninstall" to remove it again.
set -eu

here=$(cd "$(dirname "$0")" && pwd)
: "${HOME:?HOME is not set}"
data=${XDG_DATA_HOME:-$HOME/.local/share}
app=$data/ebb
bin=$HOME/.local/bin
menu=$data/applications/ebb.desktop
icon=$data/icons/hicolor/scalable/apps/ebb.svg

if [ "${1:-}" = "--uninstall" ]; then
    rm -rf "$app"
    rm -f "$bin/ebb" "$menu" "$icon"
    echo "Ebb is removed. Its cleanup log stays in $data/Ebb."
    exit 0
fi

mkdir -p "$app" "$bin" "$(dirname "$menu")" "$(dirname "$icon")"
cp -R "$here/app/." "$app/"
chmod +x "$app/ebb"
ln -sf "$app/ebb" "$bin/ebb"
cp "$here/ebb.svg" "$icon"
sed "s|^Exec=.*|Exec=$app/ebb|" "$here/ebb.desktop" > "$menu"
if command -v update-desktop-database > /dev/null 2>&1; then
    update-desktop-database "$(dirname "$menu")" > /dev/null 2>&1 || true
fi

echo "Ebb is installed. Open it from your app menu, or run: ebb"
case ":$PATH:" in
    *":$bin:"*) ;;
    *) echo "(Add $bin to your PATH to run it by name.)" ;;
esac
