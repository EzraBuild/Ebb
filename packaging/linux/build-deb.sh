#!/bin/sh
# Builds a .deb from a Linux build of Ebb (the release job runs this).
# Usage: build-deb.sh <app folder> <version> <amd64|arm64> <out.deb>
set -eu

app=$1
version=$2
arch=$3
out=$4
here=$(cd "$(dirname "$0")" && pwd)
root=$(mktemp -d)
trap 'rm -rf "$root"' EXIT

mkdir -p "$root/DEBIAN" "$root/opt/ebb" "$root/usr/bin" "$root/usr/share/applications" \
    "$root/usr/share/icons/hicolor/scalable/apps" "$root/usr/share/doc/ebb"
cp -R "$app/." "$root/opt/ebb/"
chmod 755 "$root/opt/ebb/ebb"
ln -s /opt/ebb/ebb "$root/usr/bin/ebb"
sed "s|^Exec=.*|Exec=/opt/ebb/ebb|" "$here/ebb.desktop" > "$root/usr/share/applications/ebb.desktop"
cp "$here/ebb.svg" "$root/usr/share/icons/hicolor/scalable/apps/ebb.svg"
cp "$here/../../LICENSE" "$root/usr/share/doc/ebb/copyright"

# Plain permissions whatever the build machine had: folders 755, files 644, the two programs 755.
find "$root" -type d -exec chmod 755 {} +
find "$root" -type f -exec chmod 644 {} +
chmod 755 "$root/opt/ebb/ebb"
[ -f "$root/opt/ebb/createdump" ] && chmod 755 "$root/opt/ebb/createdump"

cat > "$root/DEBIAN/control" << EOF
Package: ebb
Version: $version
Architecture: $arch
Maintainer: EzraBuild <EzraBuild@users.noreply.github.com>
Installed-Size: $(du -sk "$root" | cut -f1)
Depends: libc6, libgcc-s1, libstdc++6, zlib1g, libfontconfig1, libx11-6, libice6, libsm6
Recommends: pkexec | policykit-1
Section: utils
Priority: optional
Homepage: https://ebb.justsee.me
Description: See what's filling your disk and clean it up safely
 Ebb scans a disk, explains what's using the space in plain words, and
 cleans up only what you approve: package caches, logs, app caches,
 node_modules, old Snap versions and the Trash.
EOF

dpkg-deb --root-owner-group --build "$root" "$out"
