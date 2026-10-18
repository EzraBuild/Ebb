import type { ReactNode } from "react";
import { DownloadButtons } from "@/components/download-buttons";
import { ArrowIcon, GitHubIcon, Logo } from "@/components/icons";
import { PixelRain } from "@/components/pixel-rain";
import { ThemeToggle } from "@/components/theme-toggle";
import { Badge, Code, DownloadButton, SecondaryButton, Section, SectionHead, Shot, type Verdict } from "@/components/ui";
import { getRelease, onAur, repoUrl, type Download, type Release } from "@/lib/release";

export default async function Home() {
  const [release, aur] = await Promise.all([getRelease(), onAur()]);

  return (
    <>
      <Header />
      <main className="mx-auto max-w-[1200px] border-x border-line">
        <Hero release={release} />
        <Finds />
        <Verdicts />
        <HowItWorks />
        <Safety />
        <Disks />
        <Linux release={release} aur={aur} />
        <Faq release={release} />
        <Cta release={release} />
      </main>
      <Footer />
    </>
  );
}

function Header() {
  return (
    <header className="sticky top-0 z-50 border-b border-line bg-bg/80 backdrop-blur-md">
      <div className="mx-auto flex h-14 max-w-[1200px] items-center justify-between border-x border-line pr-3 pl-6">
        <a href="#" className="flex items-center gap-2.5 font-medium tracking-tight">
          <Logo className="size-5" />
          Ebb
        </a>
        <nav className="hidden items-center gap-8 text-sm text-muted md:flex">
          <a href="#finds" className="transition-colors hover:text-fg">What it finds</a>
          <a href="#how" className="transition-colors hover:text-fg">How it works</a>
          <a href="#safety" className="transition-colors hover:text-fg">Safety</a>
          <a href="#linux" className="transition-colors hover:text-fg">Linux</a>
          <a href="#faq" className="transition-colors hover:text-fg">FAQ</a>
        </nav>
        <div className="flex items-center gap-1">
          <ThemeToggle />
          <a href={repoUrl} aria-label="Ebb on GitHub" className="grid size-9 place-items-center text-muted transition-colors hover:text-fg">
            <GitHubIcon className="size-4" />
          </a>
          <a href="#download" className="ml-2 inline-flex h-8 items-center bg-fg px-3 text-sm font-medium text-bg transition-opacity hover:opacity-85">
            Download
          </a>
        </div>
      </div>
    </header>
  );
}

function Hero({ release }: { release: Release }) {
  return (
    <section className="relative overflow-hidden border-b border-line">
      <PixelRain className="absolute inset-x-0 top-0 h-[760px] w-full [mask-image:linear-gradient(to_bottom,black_45%,transparent)]" />
      <div className="relative px-6 pt-20 pb-16 md:px-12 md:pt-28 md:pb-20">
        <p className="inline-flex items-center gap-2 border border-line bg-bg px-2.5 py-1 font-mono text-xs text-muted">
          <span className="size-1.5 bg-fg" />
          {release.version ? `${release.version} is out` : "Now available"} · Windows and Linux · free and open source
        </p>
        <h1 className="mt-7 max-w-4xl text-5xl font-medium tracking-tighter text-balance sm:text-6xl md:text-7xl md:leading-[1.02]">
          Why is my C:&nbsp;drive full?
        </h1>
        <p className="mt-6 max-w-xl text-lg text-pretty text-muted">
          Ebb scans your drive and tells you, in plain words, what&apos;s using the space and whether it&apos;s safe to
          remove. Then it cleans up only what you approve.
        </p>
        <div className="mt-9 flex flex-wrap items-center gap-3">
          <DownloadButtons release={release} />
        </div>
        <p className="mt-5 font-mono text-xs text-subtle">
          Windows 10/11 or 64-bit Linux · nothing to install ·{" "}
          <a href={repoUrl} className="underline decoration-line-strong underline-offset-4 hover:text-fg">
            source on GitHub
          </a>
        </p>
      </div>
      <div className="relative px-3 md:px-12">
        <div className="border-x border-t border-line-strong bg-bg p-1.5 md:p-2">
          <Shot name="hero" alt="Ebb listing what's using space on C:, with each finding marked safe, check first or keep" width={2480} height={1600} sizes="(min-width: 1200px) 1100px, 100vw" />
        </div>
      </div>
    </section>
  );
}

const finds: { name: string; items: ReactNode[] }[] = [
  {
    name: "Developer",
    items: [
      <>WSL disks (<Code>ext4.vhdx</Code>)</>,
      "Docker disk and leftovers",
      <><Code>node_modules</Code> in your projects</>,
      "npm, NuGet, pip, Yarn, pnpm caches",
      "Older .NET SDKs",
      "Visual Studio caches",
    ],
  },
  {
    name: "Windows",
    items: [
      "Temporary files",
      "Windows Update downloads",
      <>Upgrade leftovers, <Code>Windows.old</Code></>,
      "Old system file versions (WinSxS)",
      "Hibernation file, page file",
      "Recycle Bin",
    ],
  },
  {
    name: "Personal",
    items: ["Old installers and zips in Downloads", "Large videos", "Exact duplicate files"],
  },
];

function Finds() {
  return (
    <Section id="finds">
      <SectionHead label="What it finds" title="Not just where the space went. What it is.">
        Most disk tools show you big folders. Ebb recognises what&apos;s inside them, especially the developer files other
        tools can&apos;t explain: WSL and Docker disks, <Code>node_modules</Code>, package caches and old SDKs.
      </SectionHead>
      <div className="grid border-t border-line md:grid-cols-3">
        {finds.map((column, i) => (
          <div key={column.name} className={`p-6 md:p-10 ${i > 0 ? "border-t border-line md:border-t-0 md:border-l" : ""}`}>
            <h3 className="font-mono text-xs tracking-widest text-subtle uppercase">{column.name}</h3>
            <ul className="mt-6 space-y-3.5">
              {column.items.map((item, j) => (
                <li key={j} className="flex gap-3 text-[15px]">
                  <span className="mt-[9px] size-1.5 shrink-0 bg-fg" />
                  <span>{item}</span>
                </li>
              ))}
            </ul>
          </div>
        ))}
      </div>
      <div className="border-t border-line px-6 py-5 font-mono text-xs text-subtle md:px-12">
        Anything else? The Biggest folders view lets you browse the rest.
      </div>
    </Section>
  );
}

const verdicts: { verdict: Verdict; title: string; text: string }[] = [
  { verdict: "safe", title: "Fine to remove", text: "It comes back by itself or isn't needed. Caches, temp files, node_modules." },
  { verdict: "check", title: "Look first", text: "Probably fine, but it's yours or it changes something. Old installers, big videos, duplicates." },
  { verdict: "keep", title: "Don't delete it", text: "You need it, but there may still be a safe fix, like shrinking a WSL or Docker disk." },
];

function Verdicts() {
  return (
    <Section>
      <SectionHead label="Every finding has a verdict" title="Safe, check first or keep. With the reason.">
        Each card says what the thing is, why it&apos;s there and what cleaning it would do, before you tick anything.
      </SectionHead>
      <div className="grid border-t border-line md:grid-cols-3">
        {verdicts.map((v, i) => (
          <div key={v.verdict} className={`p-6 md:p-10 ${i > 0 ? "border-t border-line md:border-t-0 md:border-l" : ""}`}>
            <Badge verdict={v.verdict} />
            <h3 className="mt-5 text-lg font-medium tracking-tight">{v.title}</h3>
            <p className="mt-2 text-pretty text-muted">{v.text}</p>
          </div>
        ))}
      </div>
    </Section>
  );
}

const steps = [
  { title: "Tick what you want to clean", text: "Or press Tick everything safe. Nothing happens yet." },
  {
    title: "Review the exact plan",
    text: "Every file and folder with its size and a tick box, plus the exact command for anything that runs one. Long lists have a filter: type Desktop/count, then Untick these.",
  },
  { title: "Clean up", text: "Ebb does only what you approved, then shows what it freed and writes it all to a log." },
];

function HowItWorks() {
  return (
    <Section id="how">
      <SectionHead label="How it works" title="Nothing changes until you press Clean up." />
      <div className="grid border-t border-line lg:grid-cols-[2fr_3fr]">
        <ol>
          {steps.map((step, i) => (
            <li key={step.title} className={`flex gap-5 p-6 md:p-10 ${i > 0 ? "border-t border-line" : ""}`}>
              <span className="font-mono text-sm text-subtle">0{i + 1}</span>
              <div>
                <h3 className="text-lg font-medium tracking-tight">{step.title}</h3>
                <p className="mt-2 text-pretty text-muted">{step.text}</p>
              </div>
            </li>
          ))}
        </ol>
        <div className="border-t border-line bg-card p-4 md:p-8 lg:border-t-0 lg:border-l">
          <div className="border border-line-strong">
            <Shot name="review" alt="The review window: a filter narrows node_modules to three projects, all unticked" width={1760} height={1480} sizes="(min-width: 1024px) 640px, 100vw" />
          </div>
        </div>
      </div>
    </Section>
  );
}

const rules: { title: string; body: ReactNode }[] = [
  {
    title: "Recycle Bin by default",
    body: "Untick it to delete for good. Windows asks before deleting anything too big for the bin.",
  },
  {
    title: "node_modules, fast",
    body: "Deleted for good, many files at a time: about 10× faster than one by one. A folder a dev server is using is left alone. pnpm links are removed, never followed.",
  },
  {
    title: "Each tool's own cleanup",
    body: (
      <span className="mt-1 block font-mono text-[13px] leading-6 text-fg">
        docker system prune -f<br />
        dotnet nuget locals all --clear<br />
        npm cache clean --force<br />
        pip cache purge<br />
        yarn cache clean<br />
        pnpm store prune
      </span>
    ),
  },
  {
    title: "Never touches system files",
    body: "Windows, Program Files and ProgramData are off limits. System leftovers go through Windows' own tools: Disk Cleanup, DISM and powercfg.",
  },
  {
    title: "Checked twice",
    body: "A path guard checks every item when the plan is made, and again right before it's removed. It refuses drive roots, your main folders, other users' files, system files and links.",
  },
  {
    title: "Everything is logged",
    body: (
      <>
        Every removal is written to <Code>%LOCALAPPDATA%\Ebb\cleanup-log.jsonl</Code>, so you can always see what went.
      </>
    ),
  },
];

function Safety() {
  return (
    <Section id="safety">
      <SectionHead label="Safety" title="Built so you can't lose what matters.">
        Cleaning tools have one job they must never get wrong. These rules are in the code, not in a settings page.
      </SectionHead>
      <div className="grid gap-px border-t border-line bg-line md:grid-cols-2 lg:grid-cols-3">
        {rules.map((rule) => (
          <div key={rule.title} className="bg-bg p-6 md:p-10">
            <h3 className="font-medium tracking-tight">{rule.title}</h3>
            <div className="mt-2 text-pretty text-muted">{rule.body}</div>
          </div>
        ))}
      </div>
    </Section>
  );
}

function Disks() {
  return (
    <Section>
      <div className="grid lg:grid-cols-2">
        <SectionHead label="WSL and Docker" title="Get back the space Linux already gave up.">
          WSL and Docker keep everything in a <Code>.vhdx</Code> file that grows but never shrinks, even after you delete
          things inside. Ebb compacts it and deletes nothing. For Docker, quit Docker Desktop first.
        </SectionHead>
        <div className="border-t border-line p-4 md:p-8 lg:border-t-0 lg:border-l">
          <div className="border border-line-strong bg-card font-mono text-[13px]">
            <div className="flex items-center justify-between border-b border-line px-4 py-2.5 text-xs text-subtle">
              <span>Shrink WSL disk: Ubuntu</span>
              <span>3 steps</span>
            </div>
            <pre className="overflow-x-auto p-5 leading-7">
              <span className="text-subtle"># 1 · let Linux mark its free space</span>
              {"\n"}wsl -d Ubuntu -u root -e fstrim -a
              {"\n\n"}<span className="text-subtle"># 2 · stop WSL so the file is closed</span>
              {"\n"}wsl --shutdown
              {"\n\n"}<span className="text-subtle"># 3 · compact it (asks for admin)</span>
              {"\n"}Optimize-VHD -LiteralPath ext4.vhdx -Mode Full
              {"\n\n"}<span className="text-subtle"># no Hyper-V? diskpart, attached read-only</span>
              {"\n"}attach vdisk readonly · compact vdisk
            </pre>
          </div>
          <p className="mt-4 text-sm text-pretty text-muted">
            Admin steps run in <Code>Ebb.Helper.exe</Code>, a small separate program that only does three fixed things.
            Windows asks you each time.
          </p>
        </div>
      </div>
    </Section>
  );
}

const linuxFinds: ReactNode[] = [
  "pacman, apt or dnf package cache",
  "The systemd journal, keeping the newest 100 MB",
  "Unused Flatpak runtimes",
  <>App caches in <Code>~/.cache</Code></>,
  "yay and paru build files",
  "Old Snap versions",
  "The Trash, on every drive",
];

function Install({ label, command, download, file }: { label: string; command: string; download?: Download; file?: string }) {
  return (
    <div className="border-t border-line px-4 py-3.5 first:border-t-0">
      <div className="flex items-baseline justify-between gap-4 text-xs text-subtle">
        <span>{label}</span>
        {download && (
          <a href={download.url} className="underline decoration-line-strong underline-offset-4 hover:text-fg">
            {file}
            {download.size ? ` · ${download.size}` : ""}
          </a>
        )}
      </div>
      <pre className="mt-2 overflow-x-auto leading-6 whitespace-pre-wrap">{command}</pre>
    </div>
  );
}

function Linux({ release, aur }: { release: Release; aur: boolean }) {
  return (
    <Section id="linux">
      <SectionHead label="Linux" title="The same Ebb, on Linux.">
        For Linux on Intel, AMD and ARM: Arch and Omarchy, Ubuntu, Fedora and others. It knows where Linux keeps
        things, uses the Trash like your file manager does, and steps that need root ask for your password first.
      </SectionHead>
      <div className="grid border-t border-line lg:grid-cols-[2fr_3fr]">
        <div className="p-6 md:p-10">
          <h3 className="font-mono text-xs tracking-widest text-subtle uppercase">Also finds</h3>
          <ul className="mt-6 space-y-3.5">
            {linuxFinds.map((item, i) => (
              <li key={i} className="flex gap-3 text-[15px]">
                <span className="mt-[9px] size-1.5 shrink-0 bg-fg" />
                <span>{item}</span>
              </li>
            ))}
          </ul>
          <div className="mt-8 border border-line-strong bg-card font-mono text-[13px]">
            {aur && <Install label="Arch, Omarchy, Manjaro" command="yay -S ebb-bin" />}
            <Install label="Ubuntu, Debian, Mint" command="sudo apt install ./ebb_*_amd64.deb" download={release.deb} file=".deb" />
            <Install
              label="Any distro, no root needed"
              command={"tar -xzf Ebb-*-linux-x64.tar.gz\ncd Ebb-*-linux-x64 && ./install.sh"}
              download={release.linux}
              file="tarball"
            />
          </div>
          <p className="mt-4 text-sm text-pretty text-muted">
            On ARM, like a Raspberry Pi or an Asahi Mac? Get the{" "}
            <a href={release.linuxArm.url} className="underline underline-offset-4 hover:text-fg">arm64 tarball</a> or{" "}
            <a href={release.debArm.url} className="underline underline-offset-4 hover:text-fg">arm64 .deb</a>.
            {" "}<Code>./install.sh --uninstall</Code> removes the tarball install again.
          </p>
          <div className="mt-6">
            <DownloadButton download={release.linux} label="Download for Linux" />
          </div>
        </div>
        <div className="border-t border-line bg-card p-4 md:p-8 lg:border-t-0 lg:border-l">
          <div className="border border-line-strong">
            <Shot name="linux" alt="Ebb on Linux: the System page with the pacman cache, app caches and system logs" width={2480} height={1600} sizes="(min-width: 1024px) 640px, 100vw" />
          </div>
        </div>
      </div>
    </Section>
  );
}

function Faq({ release }: { release: Release }) {
  const faqs: { q: string; a: ReactNode }[] = [
    { q: "Is Ebb free?", a: "Yes. It's open source under the MIT license. No ads, no account, no upsell." },
    {
      q: "Does it work on Linux?",
      a: "Yes, on Intel, AMD and ARM: Arch and Omarchy, Ubuntu, Fedora and others. Instead of Windows' leftovers, its System page covers package caches, the journal, Flatpak, Snap and ~/.cache, and removed files go to the Trash.",
    },
    {
      q: "Why does Windows say “Windows protected your PC”?",
      a: "Ebb isn't code-signed yet, so SmartScreen doesn't recognise it. Click More info, then Run anyway. Every line of the code is on GitHub.",
    },
    {
      q: "Which download should I get?",
      a: (
        <>
          On Linux: <Code>yay -S ebb-bin</Code> on Arch, the .deb on Ubuntu or Debian, or the tarball (
          {release.linux.size ?? "about 40 MB"}) anywhere else. On Windows, the main download (
          {release.full.size ?? "about 72 MB"}) has everything it needs. If you already have the{" "}
          <a href="https://dotnet.microsoft.com/download/dotnet/8.0" className="underline underline-offset-4 hover:text-fg">
            .NET 8 Desktop Runtime
          </a>
          , the{" "}
          <a href={release.small.url} className="underline underline-offset-4 hover:text-fg">
            small one
          </a>{" "}
          ({release.small.size ?? "about 3 MB"}) does the same thing.
        </>
      ),
    },
    {
      q: "Can I undo a cleanup?",
      a: "Files go to the Recycle Bin (the Trash on Linux) by default, so you can restore them from there. node_modules is deleted for good, but npm install brings it back. Everything removed is in the cleanup log.",
    },
    {
      q: "Does it need admin rights?",
      a: "On Windows, only to shrink a WSL or Docker disk, turn hibernation off, or run Windows' component cleanup, and Windows asks you each time. On Linux, cleaning the package cache or system logs runs that tool through pkexec, which asks for your password. Everything else runs as you.",
    },
    {
      q: "Does it send my data anywhere?",
      a: "No. Ebb doesn't connect to the internet. No account, no tracking, no telemetry.",
    },
  ];

  return (
    <Section id="faq">
      <div className="grid lg:grid-cols-[2fr_3fr]">
        <SectionHead label="FAQ" title="Questions" />
        <div className="border-t border-line lg:border-t-0 lg:border-l">
          {faqs.map((faq, i) => (
            <details key={faq.q} className={`group ${i > 0 ? "border-t border-line" : ""}`}>
              <summary className="flex cursor-pointer items-center justify-between gap-6 px-6 py-5 font-medium md:px-10">
                {faq.q}
                <span className="font-mono text-subtle transition-transform group-open:rotate-45">+</span>
              </summary>
              <p className="-mt-1 px-6 pb-6 text-pretty text-muted md:px-10">{faq.a}</p>
            </details>
          ))}
        </div>
      </div>
    </Section>
  );
}

function Cta({ release }: { release: Release }) {
  return (
    <Section id="download" className="border-b-0">
      <PixelRain density={0.7} className="absolute inset-0 size-full [mask-image:linear-gradient(to_bottom,transparent,black_30%,black_70%,transparent)]" />
      <div className="relative px-6 py-24 text-center md:py-32">
        <h2 className="mx-auto max-w-2xl text-4xl font-medium tracking-tighter text-balance md:text-6xl">
          See what&apos;s filling your drive.
        </h2>
        <p className="mx-auto mt-5 max-w-md text-balance text-muted">One scan, a plain answer, and nothing deleted until you say so.</p>
        <div className="mt-9 flex flex-wrap justify-center gap-3">
          <DownloadButtons release={release} />
          <SecondaryButton href={`${repoUrl}/releases`}>
            All releases
            <ArrowIcon className="size-4" />
          </SecondaryButton>
        </div>
      </div>
    </Section>
  );
}

function Footer() {
  return (
    <footer className="border-t border-line">
      <div className="mx-auto flex max-w-[1200px] flex-col gap-4 border-x border-line px-6 py-8 text-sm text-muted md:flex-row md:items-center md:justify-between md:px-12">
        <div className="flex items-center gap-2.5">
          <Logo className="size-4" />
          <span className="text-fg">Ebb</span>
          <span>· MIT license</span>
        </div>
        <div className="flex gap-6">
          <a href={repoUrl} className="hover:text-fg">GitHub</a>
          <a href={`${repoUrl}/releases`} className="hover:text-fg">Releases</a>
          <a href={`${repoUrl}/issues`} className="hover:text-fg">Report a problem</a>
        </div>
      </div>
    </footer>
  );
}
