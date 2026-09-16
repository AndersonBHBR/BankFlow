import type { Metadata } from "next";
import "./globals.css";

export const metadata: Metadata = {
  title: {
    default: "BankFlow Operations Console",
    template: "%s | BankFlow",
  },
  description:
    "Laboratório de core banking com contas, Pix, razão contábil e arquitetura orientada a eventos.",
  robots: {
    index: true,
    follow: true,
  },
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="pt-BR">
      <body>{children}</body>
    </html>
  );
}
