import type { Metadata } from "next";
import { Noto_Sans_JP, Bebas_Neue } from "next/font/google";
import "./globals.css";

const notoSans = Noto_Sans_JP({
  subsets: ["latin"],
  weight: ["400", "700", "900"],
  variable: "--font-noto",
  display: "swap",
});

const bebas = Bebas_Neue({
  subsets: ["latin"],
  weight: ["400"],
  variable: "--font-bebas",
  display: "swap",
});

export const metadata: Metadata = {
  title: "Escape Nine — App Store Screenshots",
  description: "Generator for Escape Nine: Endless App Store screenshots",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html
      lang="ja"
      className={`${notoSans.variable} ${bebas.variable} h-full antialiased`}
    >
      <body className="min-h-full bg-neutral-950 text-neutral-100">
        {children}
      </body>
    </html>
  );
}
