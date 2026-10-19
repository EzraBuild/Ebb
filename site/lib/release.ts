import { cacheLife } from "next/cache";

export const repoUrl = "https://github.com/EzraBuild/Ebb";
export const releasesUrl = `${repoUrl}/releases/latest`;

export type Download = {
  url: string;
  /** e.g. "72 MB", or null when GitHub couldn't be reached. */
  size: string | null;
};

export type Release = {
  version: string | null;
  /** Needs nothing installed. */
  full: Download;
  /** Needs the .NET 8 Desktop Runtime. */
  small: Download;
  /** 64-bit Linux, nothing to install. */
  linux: Download;
  /** ARM Linux (Raspberry Pi, Asahi). */
  linuxArm: Download;
  /** Ubuntu and Debian packages. */
  deb: Download;
  debArm: Download;
  /** Fedora and openSUSE packages. */
  rpm: Download;
  rpmArm: Download;
};

type Asset = { name: string; size: number; browser_download_url: string };

const fallback: Release = {
  version: null,
  full: { url: releasesUrl, size: null },
  small: { url: releasesUrl, size: null },
  linux: { url: releasesUrl, size: null },
  linuxArm: { url: releasesUrl, size: null },
  deb: { url: releasesUrl, size: null },
  debArm: { url: releasesUrl, size: null },
  rpm: { url: releasesUrl, size: null },
  rpmArm: { url: releasesUrl, size: null },
};

/** The newest GitHub release, checked about once an hour. Falls back to the releases page. */
export async function getRelease(): Promise<Release> {
  "use cache";
  cacheLife("hours");

  try {
    const response = await fetch("https://api.github.com/repos/EzraBuild/Ebb/releases/latest", {
      headers: { Accept: "application/vnd.github+json", "User-Agent": "ebb-site" },
    });
    if (!response.ok) return fallback;

    const release: { tag_name: string; assets: Asset[] } = await response.json();
    const pick = (suffix: string): Download => {
      const asset = release.assets.find((a) => a.name.endsWith(suffix));
      return asset ? { url: asset.browser_download_url, size: megabytes(asset.size) } : fallback.full;
    };

    return {
      version: release.tag_name,
      full: pick("-win-x64.zip"),
      small: pick("-needs-dotnet8.zip"),
      linux: pick("-linux-x64.tar.gz"),
      linuxArm: pick("-linux-arm64.tar.gz"),
      deb: pick("_amd64.deb"),
      debArm: pick("_arm64.deb"),
      rpm: pick(".x86_64.rpm"),
      rpmArm: pick(".aarch64.rpm"),
    };
  } catch {
    return fallback;
  }
}

/** Whether ebb-bin is on the AUR yet, so the site only shows "yay -S ebb-bin" once it works. */
export async function onAur(): Promise<boolean> {
  "use cache";
  cacheLife("hours");

  try {
    const response = await fetch("https://aur.archlinux.org/rpc/v5/info?arg%5B%5D=ebb-bin", {
      headers: { "User-Agent": "ebb-site" },
    });
    const info: { resultcount?: number } = await response.json();
    return (info.resultcount ?? 0) > 0;
  } catch {
    return false;
  }
}

function megabytes(bytes: number) {
  return `${Math.max(1, Math.round(bytes / 1024 / 1024))} MB`;
}
