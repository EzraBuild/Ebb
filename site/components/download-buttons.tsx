"use client";

import { useSyncExternalStore } from "react";
import type { Release } from "@/lib/release";
import { DownloadButton } from "./ui";

const never = () => () => {};
const onLinux = () => navigator.userAgent.includes("Linux") && !navigator.userAgent.includes("Android");

/** Windows and Linux downloads, with the visitor's own system first. The server renders Windows first. */
export function DownloadButtons({ release }: { release: Release }) {
  const linux = useSyncExternalStore(never, onLinux, () => false);

  const windows = <DownloadButton key="windows" download={release.full} label="Download for Windows" primary={!linux} />;
  const forLinux = <DownloadButton key="linux" download={release.linux} label="Download for Linux" primary={linux} />;
  return <>{linux ? [forLinux, windows] : [windows, forLinux]}</>;
}
