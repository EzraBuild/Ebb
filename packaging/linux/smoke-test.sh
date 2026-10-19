#!/bin/sh
# Installs each Linux download the way a user would and starts Ebb on a fresh account under a virtual
# screen; it must still be running after 15 seconds. For CI machines only: it installs packages
# system-wide and needs xvfb and Docker. Usage: smoke-test.sh <folder with the downloads> <version> <x64|arm64>
set -eu

dist=$(cd "$1" && pwd)
version=$2
arch=$3
if [ "$arch" = x64 ]; then deb_arch=amd64 rpm_arch=x86_64; else deb_arch=arm64 rpm_arch=aarch64; fi

starts() {
    what=$1
    shift
    home=$(mktemp -d)
    set +e
    HOME="$home" timeout 15 xvfb-run -a "$@"
    code=$?
    set -e
    cat "$home/.local/share/Ebb/errors.log" 2> /dev/null || true
    if [ "$code" -ne 124 ]; then
        echo "FAILED: $what (exit $code)"
        exit 1
    fi
    echo "ok: $what"
}

unpacked=$(mktemp -d)
tar -xzf "$dist/Ebb-$version-linux-$arch.tar.gz" -C "$unpacked"
starts "tarball" "$unpacked/Ebb-$version-linux-$arch/app/ebb"

sudo apt-get install -y -qq "$dist/ebb_${version}_${deb_arch}.deb" > /dev/null
starts ".deb, installed with apt" ebb
sudo apt-get remove -y -qq ebb > /dev/null

# Fedora, in a container: dnf has to find every library the .rpm asks for.
docker run --rm -v "$dist:/dist" fedora:latest sh -c "
    dnf install -y -q /dist/ebb-${version}-1.${rpm_arch}.rpm xorg-x11-server-Xvfb > /dev/null
    Xvfb :99 > /dev/null 2>&1 &
    sleep 2
    HOME=\$(mktemp -d) DISPLAY=:99 timeout 15 ebb
    code=\$?
    if [ \$code -ne 124 ]; then echo 'FAILED: .rpm, installed with dnf on Fedora (exit '\$code')'; exit 1; fi
    echo 'ok: .rpm, installed with dnf on Fedora'"
