"use client";

import { useEffect } from "react";
import { MoonIcon, SunIcon } from "./icons";

const system = () => (matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light");

/** Runs before the page paints, so there's no flash of the wrong theme. */
export const themeScript = `try{var t=localStorage.getItem("theme");document.documentElement.dataset.theme=t==="light"||t==="dark"?t:matchMedia("(prefers-color-scheme: dark)").matches?"dark":"light"}catch(e){}`;

export function ThemeToggle() {
  // Follow the system until the visitor picks a theme.
  useEffect(() => {
    const media = matchMedia("(prefers-color-scheme: dark)");
    const follow = () => {
      if (!stored()) document.documentElement.dataset.theme = system();
    };
    media.addEventListener("change", follow);
    return () => media.removeEventListener("change", follow);
  }, []);

  const toggle = () => {
    const next = document.documentElement.dataset.theme === "dark" ? "light" : "dark";
    document.documentElement.dataset.theme = next;
    try {
      localStorage.setItem("theme", next);
    } catch {}
  };

  return (
    <button
      type="button"
      onClick={toggle}
      aria-label="Switch between light and dark"
      className="grid size-9 cursor-pointer place-items-center text-muted transition-colors hover:text-fg"
    >
      <SunIcon className="hidden size-4 dark:block" />
      <MoonIcon className="size-4 dark:hidden" />
    </button>
  );
}

function stored() {
  try {
    return localStorage.getItem("theme");
  } catch {
    return null;
  }
}
