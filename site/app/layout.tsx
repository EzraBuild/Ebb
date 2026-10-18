import type { Metadata } from "next";
import localFont from "next/font/local";
import { themeScript } from "@/components/theme-toggle";
import "./globals.css";

// The same Geist files the app ships with (SIL Open Font License, see fonts/OFL.txt).
const geistSans = localFont({
  variable: "--font-geist-sans",
  src: [
    { path: "./fonts/Geist-Regular.ttf", weight: "400" },
    { path: "./fonts/Geist-Medium.ttf", weight: "500" },
    { path: "./fonts/Geist-SemiBold.ttf", weight: "600" },
  ],
});

const geistMono = localFont({
  variable: "--font-geist-mono",
  src: [
    { path: "./fonts/GeistMono-Regular.ttf", weight: "400" },
    { path: "./fonts/GeistMono-Medium.ttf", weight: "500" },
  ],
});

const site = process.env.VERCEL_PROJECT_PRODUCTION_URL
  ? `https://${process.env.VERCEL_PROJECT_PRODUCTION_URL}`
  : "http://localhost:3000";

const description =
  "Ebb scans your Windows drive, explains what's using the space in plain words, and helps you clean it up safely. Free and open source.";

export const metadata: Metadata = {
  metadataBase: new URL(site),
  title: "Ebb · Why is my C: drive full?",
  description,
  openGraph: {
    title: "Ebb · Why is my C: drive full?",
    description,
    images: [{ url: "/shots/hero-dark.png", width: 2480, height: 1600, alt: "Ebb showing what's using space on C:" }],
  },
  twitter: { card: "summary_large_image" },
};

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html lang="en" suppressHydrationWarning className={`${geistSans.variable} ${geistMono.variable} h-full antialiased`}>
      <head>
        <script dangerouslySetInnerHTML={{ __html: themeScript }} />
      </head>
      <body className="min-h-full font-sans">{children}</body>
    </html>
  );
}
