#!/usr/bin/env bash
# Installs GitPeach for the current user, without root: the binary in ~/.local/opt/gitpeach (so the app can update
# itself there), a "gitpeach" command in ~/.local/bin and a menu entry, so the desktop's app search finds it.
# Run it from the extracted release:  ./install.sh      Undo with:  ~/.local/opt/gitpeach/uninstall.sh
set -euo pipefail
src="$(cd "$(dirname "$0")" && pwd)"
data="${XDG_DATA_HOME:-$HOME/.local/share}"
dir="$HOME/.local/opt/gitpeach"
bin="$HOME/.local/bin"

if [[ ! -f "$src/GitPeach" ]]; then
  echo "No se encontró el binario GitPeach junto a install.sh." >&2
  exit 1
fi

mkdir -p "$dir" "$bin" "$data/applications" "$data/icons/hicolor/256x256/apps"
# Through a temporary name: replacing a running binary in place would break it, a rename does not.
install -m 755 "$src/GitPeach" "$dir/GitPeach.tmp"
mv -f "$dir/GitPeach.tmp" "$dir/GitPeach"
install -m 755 "$src/uninstall.sh" "$dir/uninstall.sh"
install -m 644 "$src/installed.txt" "$dir/installed.txt"
install -m 644 "$src/gitpeach.png" "$data/icons/hicolor/256x256/apps/gitpeach.png"
ln -sf "$dir/GitPeach" "$bin/gitpeach"

cat > "$data/applications/gitpeach.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=GitPeach
GenericName=Git client
GenericName[es]=Cliente de git
Comment=Git client with history graph, diffs and branches
Comment[es]=Cliente de git con grafo de historial, diffs y ramas
Exec="$dir/GitPeach" %F
Icon=gitpeach
Terminal=false
Categories=Development;RevisionControl;
Keywords=git;commit;branch;diff;merge;
EOF

command -v update-desktop-database >/dev/null && update-desktop-database -q "$data/applications" || true
command -v gtk-update-icon-cache >/dev/null && gtk-update-icon-cache -q -t "$data/icons/hicolor" || true

echo "GitPeach instalado en $dir"
echo "Búscalo en el menú de aplicaciones o ejecuta: gitpeach"
case ":$PATH:" in *":$bin:"*) ;; *) echo "(Agrega $bin a tu PATH para usar el comando gitpeach.)" ;; esac
echo "Para desinstalar: $dir/uninstall.sh"
