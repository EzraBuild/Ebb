"use client";

import { useEffect, useRef } from "react";

const CELL = 10; // grid step in CSS pixels
const BLOCK = 8; // block size; the gap keeps it looking like pixels

type Drop = {
  col: number;
  row: number;
  length: number;
  speed: number; // cells per second
  progress: number;
  alpha: number;
  floor: number; // row it lands on
};

type Splash = { col: number; row: number; age: number; alpha: number };

// Where a splash's two blocks are at each step, as [dx, dy] from where the drop landed.
const SPLASH = [
  [1, -1],
  [2, -2],
  [3, -2],
  [4, -1],
];

/**
 * Blocky rain on a canvas: drops snap from cell to cell and splash where they land.
 * Uses the page's --rain colour, and holds still for people who prefer less motion.
 */
export function PixelRain({ className = "", density = 1 }: { className?: string; density?: number }) {
  const ref = useRef<HTMLCanvasElement>(null);

  useEffect(() => {
    const canvas = ref.current;
    const context = canvas?.getContext("2d");
    if (!canvas || !context) return;

    let width = 0;
    let height = 0;
    let cols = 0;
    let rows = 0;
    let drops: Drop[] = [];
    let splashes: Splash[] = [];
    let colour = "250, 250, 250";
    let strength = 1;
    let frame = 0;
    let last = 0;
    let visible = true;
    const still = matchMedia("(prefers-reduced-motion: reduce)");

    const readColour = () => {
      const style = getComputedStyle(canvas);
      colour = style.getPropertyValue("--rain").trim() || colour;
      strength = Number(style.getPropertyValue("--rain-strength")) || 1;
    };

    const spawn = (anywhere: boolean): Drop => {
      const length = 3 + Math.floor(Math.random() * 8);
      const floor = rows - 1 - Math.floor(Math.random() * rows * 0.4);
      return {
        col: Math.floor(Math.random() * cols),
        row: anywhere ? Math.floor(Math.random() * floor) : -Math.floor(Math.random() * rows * 0.5),
        length,
        speed: 10 + Math.random() * 26,
        progress: 0,
        alpha: 0.12 + Math.random() * 0.33,
        floor,
      };
    };

    const resize = () => {
      const rect = canvas.getBoundingClientRect();
      const ratio = Math.min(window.devicePixelRatio || 1, 2);
      width = rect.width;
      height = rect.height;
      canvas.width = Math.round(width * ratio);
      canvas.height = Math.round(height * ratio);
      context.setTransform(ratio, 0, 0, ratio, 0, 0);
      cols = Math.ceil(width / CELL);
      rows = Math.ceil(height / CELL);
      drops = Array.from({ length: Math.round(cols * 0.2 * density) }, () => spawn(true));
      splashes = [];
      draw();
    };

    const block = (col: number, row: number, alpha: number) => {
      context.fillStyle = `rgba(${colour}, ${alpha * strength})`;
      context.fillRect(col * CELL, row * CELL, BLOCK, BLOCK);
    };

    const draw = () => {
      context.clearRect(0, 0, width, height);
      for (const drop of drops) {
        for (let i = 0; i < drop.length; i++) {
          const row = drop.row - i;
          if (row < 0 || row > drop.floor) continue;
          block(drop.col, row, drop.alpha * (1 - i / drop.length) ** 1.6);
        }
      }
      for (const splash of splashes) {
        const step = Math.min(SPLASH.length - 1, Math.floor(splash.age * 18));
        const fade = splash.alpha * (1 - splash.age / 0.3);
        const [dx, dy] = SPLASH[step];
        block(splash.col - dx, splash.row + dy, fade);
        block(splash.col + dx, splash.row + dy, fade);
      }
    };

    const update = (seconds: number) => {
      for (let i = 0; i < drops.length; i++) {
        const drop = drops[i];
        drop.progress += seconds * drop.speed;
        const steps = Math.floor(drop.progress);
        drop.progress -= steps;
        drop.row += steps;
        if (drop.row - drop.length > drop.floor) {
          if (drop.floor < rows - 1) splashes.push({ col: drop.col, row: drop.floor, age: 0, alpha: drop.alpha });
          drops[i] = spawn(false);
        }
      }
      splashes = splashes.filter((splash) => (splash.age += seconds) < 0.3);
    };

    const tick = (now: number) => {
      const seconds = last ? Math.min((now - last) / 1000, 0.1) : 0;
      last = now;
      update(seconds);
      draw();
      frame = requestAnimationFrame(tick);
    };

    const start = () => {
      cancelAnimationFrame(frame);
      last = 0;
      if (visible && !still.matches) frame = requestAnimationFrame(tick);
      else draw();
    };

    readColour();
    resize();
    start();

    const resizer = new ResizeObserver(resize);
    resizer.observe(canvas);
    const onScreen = new IntersectionObserver(([entry]) => {
      visible = entry.isIntersecting;
      start();
    });
    onScreen.observe(canvas);
    const theme = new MutationObserver(() => {
      readColour();
      draw();
    });
    theme.observe(document.documentElement, { attributes: true, attributeFilter: ["data-theme"] });
    still.addEventListener("change", start);

    return () => {
      cancelAnimationFrame(frame);
      resizer.disconnect();
      onScreen.disconnect();
      theme.disconnect();
      still.removeEventListener("change", start);
    };
  }, [density]);

  return <canvas ref={ref} aria-hidden className={`pointer-events-none ${className}`} />;
}
