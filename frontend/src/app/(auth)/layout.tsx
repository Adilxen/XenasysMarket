import type { Metadata } from "next";
import { Manrope, Source_Sans_3 } from "next/font/google";

/**
 * ==========================================================================================
 * WHAT: Auth-route typography — Manrope (display) + Source Sans 3 (UI body/labels).
 * WHY: Login Page.png uses a clean modern sans stack; avoids Arial/Geist default mismatch.
 * EXAMPLE: /login inherits these CSS variables for title, labels, and form controls.
 * ==========================================================================================
 */
const loginDisplay = Manrope({
  subsets: ["latin"],
  variable: "--font-login-display",
  weight: ["600", "700", "800"],
});

const loginSans = Source_Sans_3({
  subsets: ["latin"],
  variable: "--font-login-sans",
  weight: ["400", "500", "600", "700"],
});

export const metadata: Metadata = {
  title: "Login | Xenasys Market",
};

export default function AuthLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <div
      className={`${loginDisplay.variable} ${loginSans.variable} font-[family-name:var(--font-login-sans)] antialiased`}
    >
      {children}
    </div>
  );
}
