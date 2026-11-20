import Image from "next/image";

const FEATURES = [
  {
    title: "Manage Catalog, Inventory & Real-time Orders",
    icon: ClockIcon,
  },
  {
    title: "Instant Dispatch & Express Fulfilment Hub",
    icon: BoltIcon,
  },
  {
    title: "Escrow Protected Multi-Currency Payouts",
    icon: LockShieldIcon,
  },
] as const;

/**
 * ==========================================================================================
 * WHAT: Compact left login brand plane — smaller logo, hub title, and feature chips.
 * WHY: Keeps the Figma layout while fitting comfortably on common laptop viewports.
 * EXAMPLE: Shared /brand/xenasys-market-logo-v2.png + three condensed glass feature rows.
 * ==========================================================================================
 */
export function LoginBrandPanel() {
  return (
    <aside
      className="relative hidden min-h-screen w-full overflow-hidden lg:flex lg:w-1/2"
      aria-label="Xenasys Market brand"
    >
      <div
        className="absolute inset-0 scale-[1.03] bg-cover bg-center"
        style={{ backgroundImage: "url(/login/login-bg.png)" }}
      />
      <div className="absolute inset-0 bg-[#0A1628]/70" />
      <div className="absolute inset-0 bg-gradient-to-b from-[#07111F]/50 via-transparent to-[#050D18]/86" />

      <div className="relative z-10 flex w-full flex-col items-center justify-center px-8 py-10 text-white xl:px-12">
        <div className="flex w-full max-w-[320px] flex-col items-center text-center">
          <Image
            src="/brand/xenasys-market-logo-v2.png"
            alt="XENASYS Market — Multi-Tenant Commerce"
            width={120}
            height={42}
            priority
            unoptimized
            className="h-auto w-full max-w-[150px] object-contain drop-shadow-md xl:max-w-[165px]"
          />

          <h2 className="mt-6 font-[family-name:var(--font-login-display)] text-[18px] font-bold uppercase leading-tight tracking-[0.08em] text-white xl:text-[20px]">
            Vendor &amp; Customer Hub
          </h2>
          <p className="mt-2 max-w-xs text-[12px] italic leading-relaxed text-white/80">
            &ldquo;Empowering Sellers &amp; Buyers Everyday&rdquo;
          </p>

          <ul className="mt-6 w-full space-y-2 text-left">
            {FEATURES.map(({ title, icon: Icon }) => (
              <li
                key={title}
                className="flex items-center gap-2.5 rounded-xl border border-white/15 bg-white/[0.11] px-3 py-2.5 shadow-[0_8px_20px_rgba(0,0,0,0.2)] backdrop-blur-md"
              >
                <span className="flex h-8 w-8 shrink-0 items-center justify-center rounded-lg bg-white/10">
                  <Icon />
                </span>
                <span className="text-[11.5px] font-medium leading-snug text-white/95">
                  {title}
                </span>
              </li>
            ))}
          </ul>
        </div>
      </div>
    </aside>
  );
}

function ClockIcon() {
  return (
    <svg viewBox="0 0 24 24" className="h-4 w-4 text-white" fill="none" stroke="currentColor" strokeWidth="1.8">
      <circle cx="12" cy="12" r="8" />
      <path d="M12 8v4l2.5 1.5" strokeLinecap="round" />
    </svg>
  );
}

function BoltIcon() {
  return (
    <svg viewBox="0 0 24 24" className="h-4 w-4 text-white" fill="currentColor">
      <path d="M13 2 4 14h7l-1 8 10-14h-7l0-6z" />
    </svg>
  );
}

function LockShieldIcon() {
  return (
    <svg viewBox="0 0 24 24" className="h-4 w-4 text-white" fill="none" stroke="currentColor" strokeWidth="1.8">
      <path d="M12 3 5 6v5c0 4.5 3 8 7 9 4-1 7-4.5 7-9V6l-7-3z" />
      <rect x="9.5" y="10" width="5" height="4.5" rx="1" />
      <path d="M11 10V9a1 1 0 0 1 2 0v1" />
    </svg>
  );
}
