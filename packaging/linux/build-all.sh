#!/bin/sh
# Builds every Linux download for one kind of CPU: the tarball, the .deb and the .rpm.
# Needs the .NET 8 SDK, dpkg-deb and rpmbuild. Usage: build-all.sh <version> <x64|arm64> <out folder>
set -eu

version=$1
arch=$2
mkdir -p "$3"
dist=$(cd "$3" && pwd)
here=$(cd "$(dirname "$0")" && pwd)
repo=$(cd "$here/../.." && pwd)
name="Ebb-$version-linux-$arch"
work=$(mktemp -d)
trap 'rm -rf "$work"' EXIT

dotnet publish "$repo/src/Ebb.Linux" -c Release -r "linux-$arch" --self-contained \
    -p:Version="$version" -p:DebugType=none -o "$work/$name/app"
cp "$here/install.sh" "$here/ebb.desktop" "$here/ebb.svg" "$repo/LICENSE" "$work/$name/"
chmod +x "$work/$name/install.sh" "$work/$name/app/ebb"
tar -czf "$dist/$name.tar.gz" -C "$work" "$name"

if [ "$arch" = x64 ]; then deb_arch=amd64 rpm_arch=x86_64; else deb_arch=arm64 rpm_arch=aarch64; fi
sh "$here/build-deb.sh" "$work/$name/app" "$version" "$deb_arch" "$dist/ebb_${version}_${deb_arch}.deb"
sh "$here/build-rpm.sh" "$work/$name/app" "$version" "$dist/ebb-${version}-1.${rpm_arch}.rpm"
ls -l "$dist"
