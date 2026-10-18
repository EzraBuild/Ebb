#!/bin/sh
# Fills in packaging/aur/PKGBUILD for one release. Usage: fill.sh <version> <x64 tarball> <arm64 tarball> <out PKGBUILD>
set -eu

here=$(cd "$(dirname "$0")" && pwd)
sha() { sha256sum "$1" | cut -d' ' -f1; }
sed -e "s/@VERSION@/$1/" -e "s/@SHA256_X64@/$(sha "$2")/" -e "s/@SHA256_ARM64@/$(sha "$3")/" "$here/PKGBUILD" > "$4"
