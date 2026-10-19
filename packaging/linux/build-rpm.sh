#!/bin/sh
# Builds an .rpm from a Linux build of Ebb, for Fedora, openSUSE and the like. Run it on a machine with the
# same kind of CPU as the build (the release job does). Usage: build-rpm.sh <app folder> <version> <out.rpm>
set -eu

app=$(cd "$1" && pwd)
version=$2
out=$3
here=$(cd "$(dirname "$0")" && pwd)
top=$(mktemp -d)
trap 'rm -rf "$top"' EXIT

mkdir -p "$top/SPECS" "$top/SOURCES"
cp -R "$app" "$top/SOURCES/app"
cp "$here/ebb.desktop" "$here/ebb.svg" "$here/../../LICENSE" "$top/SOURCES/"

cat > "$top/SPECS/ebb.spec" << 'EOF'
Name:           ebb
Version:        %{ebb_version}
Release:        1
Summary:        See what's filling your disk and clean it up safely
License:        MIT
URL:            https://ebb.justsee.me

# A self-contained .NET app. What it needs is listed by library name (so it works on Fedora and openSUSE
# alike) instead of scanning it, which would also pick up optional parts like a tracing library.
AutoReqProv:    no
Requires:       libfontconfig.so.1()(64bit) libX11.so.6()(64bit) libICE.so.6()(64bit) libSM.so.6()(64bit)
Requires:       libz.so.1()(64bit) libstdc++.so.6()(64bit)
Recommends:     polkit

# Stripping or splitting debug info would break it.
%global debug_package %{nil}
%global __strip /bin/true
%define _build_id_links none

%description
Ebb scans a disk, explains what's using the space in plain words, and
cleans up only what you approve: package caches, logs, app caches,
node_modules, old Snap versions and the Trash.

%install
mkdir -p %{buildroot}/opt/ebb %{buildroot}/usr/bin %{buildroot}/usr/share/applications \
    %{buildroot}/usr/share/icons/hicolor/scalable/apps %{buildroot}/usr/share/licenses/ebb
cp -R %{_sourcedir}/app/. %{buildroot}/opt/ebb/
ln -s /opt/ebb/ebb %{buildroot}/usr/bin/ebb
sed 's|^Exec=.*|Exec=/opt/ebb/ebb|' %{_sourcedir}/ebb.desktop > %{buildroot}/usr/share/applications/ebb.desktop
cp %{_sourcedir}/ebb.svg %{buildroot}/usr/share/icons/hicolor/scalable/apps/ebb.svg
cp %{_sourcedir}/LICENSE %{buildroot}/usr/share/licenses/ebb/LICENSE

%files
/opt/ebb
/usr/bin/ebb
/usr/share/applications/ebb.desktop
/usr/share/icons/hicolor/scalable/apps/ebb.svg
/usr/share/licenses/ebb
EOF

if ! rpmbuild --define "_topdir $top" --define "ebb_version $version" -bb "$top/SPECS/ebb.spec" > "$top/build.log" 2>&1; then
    cat "$top/build.log"
    exit 1
fi
cp "$top"/RPMS/*/ebb-*.rpm "$out"
