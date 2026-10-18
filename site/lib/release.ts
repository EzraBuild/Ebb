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
};

type Asset = { name: string; size: number; browser_download_url: string };

const fallback: Release = {
  version: null,
  full: { url: releasesUrl, size: null },
  small: { url: releasesUrl, size: null },
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
    };
  } catch {
    return fallback;
  }
}

function megabytes(bytes: number) {
  return `${Math.max(1, Math.round(bytes / 1024 / 1024))} MB`;
}
