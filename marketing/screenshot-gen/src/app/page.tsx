"use client";

import { useEffect, useRef, useState } from "react";
import { toPng } from "html-to-image";

// ========== Constants ==========

const IPHONE_SIZES = [
  { label: '6.9"', w: 1320, h: 2868 },
  { label: '6.5"', w: 1284, h: 2778 },
  { label: '6.3"', w: 1206, h: 2622 },
  { label: '6.1"', w: 1125, h: 2436 },
] as const;

const MK_W = 1022;
const MK_H = 2082;
const SC_L = (52 / MK_W) * 100;
const SC_T = (46 / MK_H) * 100;
const SC_W = (918 / MK_W) * 100;
const SC_H = (1990 / MK_H) * 100;
const SC_RX = (126 / 918) * 100;
const SC_RY = (126 / 1990) * 100;

const COLOR = {
  bg1: "#0F0F1E",
  bg2: "#1A1A2E",
  bg3: "#0F3460",
  accent: "#FF6B35",
  gold: "#FFD700",
  text: "#F5F5F5",
  textSub: "#A0A0B0",
};

const BEBAS = "var(--font-bebas), 'Bebas Neue', Impact, sans-serif";
const NOTO = "var(--font-noto), system-ui, sans-serif";

const IMAGE_PATHS = [
  "/mockup.png",
  "/app-icon.png",
  "/screenshots/ja/home.png",
  "/screenshots/ja/settings.png",
  "/screenshots/ja/gameplay_low.png",
  "/screenshots/ja/gameplay_high.png",
  "/screenshots/ja/result.png",
  "/screenshots/ja/leaderboard.png",
];

const imageCache: Record<string, string> = {};

async function preloadAllImages() {
  await Promise.all(
    IMAGE_PATHS.map(async (path) => {
      const resp = await fetch(path);
      const blob = await resp.blob();
      const dataUrl = await new Promise<string>((resolve) => {
        const reader = new FileReader();
        reader.onloadend = () => resolve(reader.result as string);
        reader.readAsDataURL(blob);
      });
      imageCache[path] = dataUrl;
    })
  );
}

function img(path: string): string {
  return imageCache[path] || path;
}

// ========== Phone Component ==========

function Phone({
  src,
  alt,
  style,
}: {
  src: string;
  alt: string;
  style?: React.CSSProperties;
}) {
  return (
    <div
      style={{
        position: "relative",
        aspectRatio: `${MK_W}/${MK_H}`,
        ...style,
      }}
    >
      <img
        src={img("/mockup.png")}
        alt=""
        style={{ display: "block", width: "100%", height: "100%" }}
        draggable={false}
      />
      <div
        style={{
          position: "absolute",
          zIndex: 10,
          overflow: "hidden",
          left: `${SC_L}%`,
          top: `${SC_T}%`,
          width: `${SC_W}%`,
          height: `${SC_H}%`,
          borderRadius: `${SC_RX}% / ${SC_RY}%`,
        }}
      >
        <img
          src={src}
          alt={alt}
          style={{
            display: "block",
            width: "100%",
            height: "100%",
            objectFit: "cover",
            objectPosition: "top",
          }}
          draggable={false}
        />
      </div>
    </div>
  );
}

// ========== Background ==========

function CosmicBackground({ tint = "neutral" }: { tint?: "neutral" | "warm" | "gold" }) {
  const tints: Record<string, string> = {
    neutral: `radial-gradient(ellipse at 50% 0%, ${COLOR.bg3} 0%, ${COLOR.bg2} 35%, ${COLOR.bg1} 100%)`,
    warm: `radial-gradient(ellipse at 50% 0%, #3a1e2e 0%, ${COLOR.bg2} 40%, ${COLOR.bg1} 100%)`,
    gold: `radial-gradient(ellipse at 50% 0%, #3a2e1a 0%, ${COLOR.bg2} 40%, ${COLOR.bg1} 100%)`,
  };
  return <div style={{ position: "absolute", inset: 0, background: tints[tint] }} />;
}

// ========== Slides ==========

function Slide1Hero({ W }: { W: number }) {
  return (
    <div style={{ position: "relative", width: "100%", height: "100%", overflow: "hidden", background: COLOR.bg1 }}>
      <CosmicBackground tint="neutral" />
      <div
        style={{
          position: "absolute",
          top: "8%",
          left: "50%",
          transform: "translateX(-50%)",
          fontFamily: BEBAS,
          fontSize: W * 0.55,
          color: COLOR.accent,
          opacity: 0.06,
          lineHeight: 1,
          userSelect: "none",
        }}
      >
        100
      </div>

      <div
        style={{
          position: "absolute",
          top: W * 0.08,
          left: "50%",
          transform: "translateX(-50%)",
          display: "flex",
          alignItems: "center",
          gap: W * 0.025,
        }}
      >
        <img
          src={img("/app-icon.png")}
          alt="Escape Nine"
          style={{
            width: W * 0.1,
            height: W * 0.1,
            borderRadius: W * 0.022,
            boxShadow: "0 8px 32px rgba(0,0,0,0.5)",
          }}
        />
        <span
          style={{
            fontFamily: BEBAS,
            fontSize: W * 0.05,
            letterSpacing: W * 0.002,
            color: COLOR.gold,
          }}
        >
          ESCAPE NINE
        </span>
      </div>

      <div
        style={{
          position: "absolute",
          top: W * 0.2,
          left: "50%",
          transform: "translateX(-50%)",
          textAlign: "center",
          width: "96%",
          fontFamily: NOTO,
          whiteSpace: "nowrap",
        }}
      >
        <div style={{ fontSize: W * 0.1, fontWeight: 900, lineHeight: 1.05, color: COLOR.text }}>
          ビートに
        </div>
        <div
          style={{
            fontSize: W * 0.1,
            fontWeight: 900,
            lineHeight: 1.05,
            color: COLOR.accent,
            marginTop: W * 0.005,
          }}
        >
          合わせて逃げろ
        </div>
      </div>

      <Phone
        src={img("/screenshots/ja/home.png")}
        alt="Home"
        style={{
          position: "absolute",
          left: "50%",
          bottom: 0,
          width: `${W * 0.78}px`,
          transform: "translateX(-50%) translateY(8%)",
        }}
      />
    </div>
  );
}

function Slide2Mechanics({ W }: { W: number }) {
  return (
    <div style={{ position: "relative", width: "100%", height: "100%", overflow: "hidden", background: COLOR.bg1 }}>
      <CosmicBackground tint="neutral" />

      <div
        style={{
          position: "absolute",
          top: W * 0.1,
          left: "50%",
          transform: "translateX(-50%)",
          textAlign: "center",
          width: "92%",
          fontFamily: NOTO,
        }}
      >
        <div style={{ fontSize: W * 0.085, fontWeight: 900, lineHeight: 1.05, color: COLOR.text }}>
          9 マス、10 ターン
        </div>
        <div
          style={{
            fontSize: W * 0.085,
            fontWeight: 900,
            lineHeight: 1.05,
            color: COLOR.accent,
            marginTop: W * 0.005,
          }}
        >
          即決思考
        </div>
        <div style={{ fontSize: W * 0.028, color: COLOR.textSub, marginTop: W * 0.025 }}>
          鬼とプレイヤーが同時移動、ひと手で命運が決まる
        </div>
      </div>

      <Phone
        src={img("/screenshots/ja/gameplay_low.png")}
        alt="Gameplay Low"
        style={{
          position: "absolute",
          left: "50%",
          bottom: 0,
          width: `${W * 0.82}px`,
          transform: "translateX(-50%) translateY(12%)",
        }}
      />
    </div>
  );
}

function Slide3BPM({ W }: { W: number }) {
  return (
    <div style={{ position: "relative", width: "100%", height: "100%", overflow: "hidden", background: COLOR.bg1 }}>
      <CosmicBackground tint="warm" />

      <div
        style={{
          position: "absolute",
          top: "30%",
          left: "-5%",
          fontFamily: BEBAS,
          fontSize: W * 0.75,
          color: COLOR.accent,
          opacity: 0.12,
          lineHeight: 1,
          letterSpacing: -W * 0.01,
          userSelect: "none",
        }}
      >
        200
      </div>

      <div
        style={{
          position: "absolute",
          top: W * 0.1,
          left: "50%",
          transform: "translateX(-50%)",
          textAlign: "center",
          width: "92%",
          fontFamily: NOTO,
        }}
      >
        <div style={{ fontSize: W * 0.1, fontWeight: 900, lineHeight: 1.05, color: COLOR.text }}>
          鼓動が
        </div>
        <div
          style={{
            fontSize: W * 0.1,
            fontWeight: 900,
            lineHeight: 1.05,
            color: COLOR.accent,
            marginTop: W * 0.005,
          }}
        >
          加速する
        </div>
        <div style={{ fontSize: W * 0.028, color: COLOR.textSub, marginTop: W * 0.025 }}>
          階層上昇で BPM が 70 → 200 へ加速
        </div>
      </div>

      <Phone
        src={img("/screenshots/ja/gameplay_high.png")}
        alt="Gameplay High"
        style={{
          position: "absolute",
          left: "50%",
          bottom: 0,
          width: `${W * 0.78}px`,
          transform: "translateX(-50%) translateY(10%)",
        }}
      />
    </div>
  );
}

function Slide4Progression({ W }: { W: number }) {
  return (
    <div style={{ position: "relative", width: "100%", height: "100%", overflow: "hidden", background: COLOR.bg1 }}>
      <CosmicBackground tint="gold" />

      <div
        style={{
          position: "absolute",
          top: "10%",
          left: "50%",
          transform: "translateX(-50%)",
          fontFamily: BEBAS,
          fontSize: W * 0.95,
          color: COLOR.gold,
          opacity: 0.14,
          lineHeight: 1,
          userSelect: "none",
        }}
      >
        100
      </div>

      <div
        style={{
          position: "absolute",
          top: W * 0.1,
          left: "50%",
          transform: "translateX(-50%)",
          textAlign: "center",
          width: "92%",
          fontFamily: NOTO,
        }}
      >
        <div style={{ fontSize: W * 0.1, fontWeight: 900, lineHeight: 1.05, color: COLOR.text }}>
          100 階層、
        </div>
        <div
          style={{
            fontSize: W * 0.1,
            fontWeight: 900,
            lineHeight: 1.05,
            color: COLOR.gold,
            marginTop: W * 0.005,
          }}
        >
          その先へ
        </div>
        <div style={{ fontSize: W * 0.028, color: COLOR.textSub, marginTop: W * 0.025 }}>
          自己ベスト更新で世界ランキングへ
        </div>
      </div>

      <Phone
        src={img("/screenshots/ja/result.png")}
        alt="Result"
        style={{
          position: "absolute",
          left: "50%",
          bottom: 0,
          width: `${W * 0.78}px`,
          transform: "translateX(-50%) translateY(10%)",
        }}
      />
    </div>
  );
}

function Slide5Free({ W }: { W: number }) {
  return (
    <div style={{ position: "relative", width: "100%", height: "100%", overflow: "hidden", background: COLOR.bg1 }}>
      <CosmicBackground tint="neutral" />

      <div
        style={{
          position: "absolute",
          top: W * 0.1,
          left: "50%",
          transform: "translateX(-50%)",
          textAlign: "center",
          width: "92%",
          fontFamily: NOTO,
        }}
      >
        <div style={{ fontSize: W * 0.1, fontWeight: 900, lineHeight: 1.05, color: COLOR.text }}>
          無料で
        </div>
        <div
          style={{
            fontSize: W * 0.1,
            fontWeight: 900,
            lineHeight: 1.05,
            color: COLOR.accent,
            marginTop: W * 0.005,
          }}
        >
          始められる
        </div>
        <div style={{ fontSize: W * 0.028, color: COLOR.textSub, marginTop: W * 0.025 }}>
          基本プレイ無料 / 広告非表示も選べる
        </div>
      </div>

      <Phone
        src={img("/screenshots/ja/settings.png")}
        alt="Settings"
        style={{
          position: "absolute",
          left: "50%",
          bottom: 0,
          width: `${W * 0.82}px`,
          transform: "translateX(-50%) translateY(12%)",
        }}
      />
    </div>
  );
}

// ========== Registry ==========

type Slide = {
  id: string;
  label: string;
  render: (props: { W: number }) => React.ReactNode;
};

const SLIDES: Slide[] = [
  { id: "hero", label: "1. Hero (ビートに合わせて逃げろ)", render: Slide1Hero },
  { id: "mechanics", label: "2. Mechanics (9 マス即決)", render: Slide2Mechanics },
  { id: "bpm", label: "3. BPM (鼓動が加速する)", render: Slide3BPM },
  { id: "progression", label: "4. Progression (100 階層)", render: Slide4Progression },
  { id: "free", label: "5. Free (無料で始められる)", render: Slide5Free },
];

// ========== Preview ==========

function ScreenshotPreview({
  slide,
  size,
  index,
  onExport,
}: {
  slide: Slide;
  size: { w: number; h: number };
  index: number;
  onExport: (slide: Slide, index: number, size: { w: number; h: number }) => void;
}) {
  const wrapperRef = useRef<HTMLDivElement>(null);
  const [scale, setScale] = useState(0.1);

  useEffect(() => {
    if (!wrapperRef.current) return;
    const ro = new ResizeObserver((entries) => {
      const w = entries[0].contentRect.width;
      setScale(w / size.w);
    });
    ro.observe(wrapperRef.current);
    return () => ro.disconnect();
  }, [size.w]);

  return (
    <div className="flex flex-col gap-2">
      <div
        ref={wrapperRef}
        className="relative bg-black rounded-xl overflow-hidden border border-neutral-800"
        style={{ aspectRatio: `${size.w}/${size.h}` }}
      >
        <div
          style={{
            width: size.w,
            height: size.h,
            transform: `scale(${scale})`,
            transformOrigin: "top left",
          }}
        >
          {slide.render({ W: size.w })}
        </div>
      </div>
      <div className="flex items-center justify-between text-xs">
        <span className="text-neutral-400">{slide.label}</span>
        <button
          onClick={() => onExport(slide, index, size)}
          className="px-3 py-1.5 bg-neutral-800 hover:bg-neutral-700 rounded text-neutral-100 transition"
        >
          Export
        </button>
      </div>
    </div>
  );
}

// ========== Main Page ==========

export default function ScreenshotsPage() {
  const [ready, setReady] = useState(false);
  const [sizeIdx, setSizeIdx] = useState(0);
  const [exporting, setExporting] = useState(false);
  const [exportTarget, setExportTarget] = useState<Slide | null>(null);
  const exportStageRef = useRef<HTMLDivElement>(null);
  const exportContentRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    preloadAllImages().then(() => setReady(true));
  }, []);

  const size = IPHONE_SIZES[sizeIdx];

  async function exportOne(slide: Slide, index: number, sz: { w: number; h: number }) {
    setExporting(true);
    try {
      // Render slide via React state into hidden stage
      setExportTarget(slide);
      // Wait for React to commit, fonts/images to settle
      await new Promise((r) => setTimeout(r, 700));

      const stage = exportStageRef.current;
      const content = exportContentRef.current;
      if (!stage || !content) return;

      // Bring on-screen for capture
      stage.style.left = "0px";
      stage.style.zIndex = "-1";

      const opts = { width: sz.w, height: sz.h, pixelRatio: 1, cacheBust: true };
      // Double-call trick: first warms up, second produces clean output
      await toPng(content, opts);
      const dataUrl = await toPng(content, opts);

      // Move back off-screen
      stage.style.left = "-9999px";
      stage.style.zIndex = "";

      // Trigger download
      const a = document.createElement("a");
      a.href = dataUrl;
      const indexStr = String(index + 1).padStart(2, "0");
      a.download = `${indexStr}-${slide.id}-${sz.w}x${sz.h}.png`;
      a.click();

      setExportTarget(null);
    } finally {
      setExporting(false);
    }
  }

  async function exportAll(sz: { w: number; h: number }) {
    for (let i = 0; i < SLIDES.length; i++) {
      await exportOne(SLIDES[i], i, sz);
      await new Promise((r) => setTimeout(r, 300));
    }
  }

  if (!ready) {
    return (
      <div className="flex items-center justify-center h-screen text-neutral-400">
        Loading images...
      </div>
    );
  }

  return (
    <div className="min-h-screen p-6">
      <header className="flex items-center justify-between gap-4 mb-6 flex-wrap">
        <div>
          <h1 className="text-2xl font-bold text-neutral-100">
            Escape Nine — Screenshots Generator
          </h1>
          <p className="text-sm text-neutral-400">
            Design size: {size.w}×{size.h} ({size.label})
          </p>
        </div>
        <div className="flex items-center gap-3">
          <label className="flex items-center gap-2 text-sm text-neutral-300">
            <span>Size:</span>
            <select
              value={sizeIdx}
              onChange={(e) => setSizeIdx(Number(e.target.value))}
              className="bg-neutral-800 border border-neutral-700 rounded px-3 py-1.5"
            >
              {IPHONE_SIZES.map((s, i) => (
                <option key={s.label} value={i}>
                  {s.label} ({s.w}×{s.h})
                </option>
              ))}
            </select>
          </label>
          <button
            onClick={() => exportAll(size)}
            disabled={exporting}
            className="px-4 py-1.5 bg-orange-600 hover:bg-orange-500 disabled:opacity-50 rounded text-white font-medium transition"
          >
            {exporting ? "Exporting..." : "Export all"}
          </button>
        </div>
      </header>

      <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-6">
        {SLIDES.map((s, i) => (
          <ScreenshotPreview key={s.id} slide={s} size={size} index={i} onExport={exportOne} />
        ))}
      </div>

      {/* Offscreen export stage — rendered through normal React tree */}
      <div
        ref={exportStageRef}
        style={{
          position: "absolute",
          left: "-9999px",
          top: 0,
          overflow: "hidden",
        }}
      >
        <div
          ref={exportContentRef}
          style={{
            width: size.w,
            height: size.h,
            fontFamily: NOTO,
          }}
        >
          {exportTarget && exportTarget.render({ W: size.w })}
        </div>
      </div>
    </div>
  );
}
