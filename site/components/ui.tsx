import Image from "next/image";
import type { ReactNode } from "react";
import type { Download } from "@/lib/release";
import { DownloadIcon, PlusIcon } from "./icons";

/** A full-width band with a plus mark where its top edge meets the page's side rules. */
export function Section({ id, className = "", children }: { id?: string; className?: string; children: ReactNode }) {
  return (
    <section id={id} className={`relative scroll-mt-14 border-b border-line ${className}`}>
      <PlusIcon className="absolute -top-[7px] -left-[7px] z-10 size-3.5 text-subtle" />
      <PlusIcon className="absolute -top-[7px] -right-[7px] z-10 size-3.5 text-subtle" />
      {children}
    </section>
  );
}

export function SectionHead({ label, title, children }: { label: string; title: ReactNode; children?: ReactNode }) {
  return (
    <div className="px-6 pt-16 pb-12 md:px-12 md:pt-24 md:pb-16">
      <p className="font-mono text-xs tracking-widest text-subtle uppercase">{label}</p>
      <h2 className="mt-4 max-w-2xl text-3xl font-medium tracking-tight text-balance md:text-[2.5rem] md:leading-[1.15]">
        {title}
      </h2>
      {children && <p className="mt-5 max-w-2xl text-pretty text-muted md:text-lg">{children}</p>}
    </div>
  );
}

export function DownloadButton({ download, className = "" }: { download: Download; className?: string }) {
  return (
    <a
      href={download.url}
      className={`inline-flex h-11 items-center gap-2.5 bg-fg px-5 text-sm font-medium text-bg transition-opacity hover:opacity-85 ${className}`}
    >
      <DownloadIcon className="size-4" />
      Download for Windows
      {download.size && <span className="font-mono text-xs opacity-55">{download.size}</span>}
    </a>
  );
}

export function SecondaryButton({ href, children }: { href: string; children: ReactNode }) {
  return (
    <a
      href={href}
      className="inline-flex h-11 items-center gap-2.5 border border-line-strong bg-bg px-5 text-sm font-medium transition-colors hover:border-fg"
    >
      {children}
    </a>
  );
}

export function Code({ children }: { children: ReactNode }) {
  return <code className="font-mono text-[0.9em]">{children}</code>;
}

export type Verdict = "safe" | "check" | "keep";

/** The same three badges the app shows on each finding. */
export function Badge({ verdict }: { verdict: Verdict }) {
  const style = {
    safe: "border-fg bg-fg text-bg",
    check: "border-fg",
    keep: "border-dashed border-fg",
  }[verdict];
  const text = { safe: "SAFE", check: "CHECK FIRST", keep: "KEEP" }[verdict];
  return <span className={`inline-block border px-1.5 py-px font-mono text-[11px] tracking-wide ${style}`}>{text}</span>;
}

/** A screenshot of the app, in the theme the visitor is using. */
export function Shot({ name, alt, width, height, sizes, preload = false }: {
  name: string;
  alt: string;
  width: number;
  height: number;
  sizes: string;
  preload?: boolean;
}) {
  return (
    <>
      <Image src={`/shots/${name}-light.png`} alt={alt} width={width} height={height} sizes={sizes} preload={preload} className="only-light h-auto w-full" />
      <Image src={`/shots/${name}-dark.png`} alt={alt} width={width} height={height} sizes={sizes} preload={preload} className="only-dark h-auto w-full" />
    </>
  );
}
