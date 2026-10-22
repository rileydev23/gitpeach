#!/usr/bin/env bash
# Removes what install.sh added. Your settings (projects, tabs, window) in ~/.config/peachapps/GitPeach stay.
set -euo pipefail
data="${XDG_DATA_HOME:-$HOME/.local/share}"
dir="$HOME/.local/opt/gitpeach"
config="${XDG_CONFIG_HOME:-$HOME/.config}/peachapps/GitPeach"

rm -f "$data/applications/gitpeach.desktop"
rm -f "$data/icons/hicolor/256x256/apps/gitpeach.png"
# Only our own link, never a "gitpeach" the user put there.
if [[ -L "$HOME/.local/bin/gitpeach" && "$(readlink "$HOME/.local/bin/gitpeach")" == "$dir/GitPeach" ]]; then
  rm -f "$HOME/.local/bin/gitpeach"
fi
rm -f "$dir/GitPeach" "$dir/GitPeach.new" "$dir/GitPeach.bin.new" "$dir/installed.txt" "$dir/uninstall.sh"
rmdir "$dir" 2>/dev/null || true

command -v update-desktop-database >/dev/null && update-desktop-database -q "$data/applications" || true
command -v gtk-update-icon-cache >/dev/null && gtk-update-icon-cache -q -t "$data/icons/hicolor" || true

echo "GitPeach desinstalado."
echo "Tu configuración sigue en $config (bórrala a mano si ya no la quieres)."
