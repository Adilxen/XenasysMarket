import type { NavIconName } from "@/components/layout/main-nav";

type IconProps = { className?: string };

/**
 * ==========================================================================================
 * WHAT: Outline icons for Main screen sidebar (stroke style matching the mock).
 * WHY: Avoid extra icon package; keep bundle light and visual language consistent.
 * EXAMPLE: <NavIcon name="home" className="h-4 w-4" />
 * ==========================================================================================
 */
export function NavIcon({
  name,
  className = "h-4 w-4",
}: {
  name: NavIconName;
  className?: string;
}) {
  switch (name) {
    case "home":
      return <HomeIcon className={className} />;
    case "categories":
      return <GridIcon className={className} />;
    case "subCategories":
      return <LayersIcon className={className} />;
    case "brands":
      return <TagIcon className={className} />;
    case "products":
      return <BoxIcon className={className} />;
    case "stock":
      return <WarehouseIcon className={className} />;
    case "sales":
      return <SalesIcon className={className} />;
    case "vendors":
      return <StoreIcon className={className} />;
    case "coa":
      return <BookIcon className={className} />;
    case "subsidiary":
      return <ListTreeIcon className={className} />;
    case "users":
      return <UsersIcon className={className} />;
    case "roles":
      return <ShieldIcon className={className} />;
    case "company":
      return <BuildingIcon className={className} />;
    case "settings":
      return <GearIcon className={className} />;
    case "actions":
      return <ActionsIcon className={className} />;
    case "formMap":
      return <FormMapIcon className={className} />;
    case "audit":
      return <AuditIcon className={className} />;
    default:
      return <BoxIcon className={className} />;
  }
}

function strokeProps(className?: string) {
  return {
    className,
    viewBox: "0 0 24 24",
    fill: "none" as const,
    stroke: "currentColor",
    strokeWidth: 1.7,
    strokeLinecap: "round" as const,
    strokeLinejoin: "round" as const,
    "aria-hidden": true as const,
  };
}

function HomeIcon({ className }: IconProps) {
  return (
    <svg {...strokeProps(className)}>
      <path d="M4 10.5 12 4l8 6.5V20a1 1 0 0 1-1 1h-5v-6H10v6H5a1 1 0 0 1-1-1v-9.5z" />
    </svg>
  );
}

function GridIcon({ className }: IconProps) {
  return (
    <svg {...strokeProps(className)}>
      <rect x="4" y="4" width="7" height="7" rx="1.2" />
      <rect x="13" y="4" width="7" height="7" rx="1.2" />
      <rect x="4" y="13" width="7" height="7" rx="1.2" />
      <rect x="13" y="13" width="7" height="7" rx="1.2" />
    </svg>
  );
}

function LayersIcon({ className }: IconProps) {
  return (
    <svg {...strokeProps(className)}>
      <path d="m12 3 9 5-9 5-9-5 9-5z" />
      <path d="m3 12 9 5 9-5" />
      <path d="m3 16 9 5 9-5" />
    </svg>
  );
}

function TagIcon({ className }: IconProps) {
  return (
    <svg {...strokeProps(className)}>
      <path d="M20 12.5 12.5 20a2 2 0 0 1-2.8 0L4 14.3V4h10.3l5.7 5.7a2 2 0 0 1 0 2.8z" />
      <circle cx="9" cy="9" r="1.2" fill="currentColor" stroke="none" />
    </svg>
  );
}

function BoxIcon({ className }: IconProps) {
  return (
    <svg {...strokeProps(className)}>
      <path d="M21 8.5 12 3 3 8.5v7L12 21l9-5.5v-7z" />
      <path d="M12 12v9" />
      <path d="m3 8.5 9 3.5 9-3.5" />
    </svg>
  );
}

function WarehouseIcon({ className }: IconProps) {
  return (
    <svg {...strokeProps(className)}>
      <path d="M3 21V9l9-6 9 6v12" />
      <path d="M7 21v-8h10v8" />
      <path d="M7 17h10" />
    </svg>
  );
}

function SalesIcon({ className }: IconProps) {
  return (
    <svg {...strokeProps(className)}>
      <path d="M4 19h16" />
      <path d="M7 16V9" />
      <path d="M12 16V5" />
      <path d="M17 16v-6" />
    </svg>
  );
}

function StoreIcon({ className }: IconProps) {
  return (
    <svg {...strokeProps(className)}>
      <path d="M4 10h16v11H4z" />
      <path d="M3 10 5.5 4h13L21 10" />
      <path d="M9 21v-6h6v6" />
    </svg>
  );
}

function BookIcon({ className }: IconProps) {
  return (
    <svg {...strokeProps(className)}>
      <path d="M5 4h11a3 3 0 0 1 3 3v13H8a3 3 0 0 0-3 3V4z" />
      <path d="M8 4v16" />
    </svg>
  );
}

function ListTreeIcon({ className }: IconProps) {
  return (
    <svg {...strokeProps(className)}>
      <path d="M8 6h13" />
      <path d="M8 12h13" />
      <path d="M8 18h13" />
      <path d="M3 6h.01" />
      <path d="M3 12h.01" />
      <path d="M3 18h.01" />
    </svg>
  );
}

function UsersIcon({ className }: IconProps) {
  return (
    <svg {...strokeProps(className)}>
      <circle cx="9" cy="8" r="3" />
      <path d="M3 19c0-3 2.5-5 6-5s6 2 6 5" />
      <circle cx="17" cy="9" r="2.2" />
      <path d="M21 19c0-2.2-1.5-3.8-4-4.2" />
    </svg>
  );
}

function ShieldIcon({ className }: IconProps) {
  return (
    <svg {...strokeProps(className)}>
      <path d="M12 3 5 6v5c0 4.5 3 8 7 9 4-1 7-4.5 7-9V6l-7-3z" />
      <path d="m9.5 12 1.8 1.8 3.7-3.7" />
    </svg>
  );
}

function BuildingIcon({ className }: IconProps) {
  return (
    <svg {...strokeProps(className)}>
      <path d="M4 21V5a1 1 0 0 1 1-1h9a1 1 0 0 1 1 1v16" />
      <path d="M15 10h4a1 1 0 0 1 1 1v10" />
      <path d="M8 8h3M8 12h3M8 16h3" />
    </svg>
  );
}

function GearIcon({ className }: IconProps) {
  return (
    <svg {...strokeProps(className)}>
      <circle cx="12" cy="12" r="3" />
      <path d="M12 3v2.2M12 18.8V21M4.9 4.9l1.6 1.6M17.5 17.5l1.6 1.6M3 12h2.2M18.8 12H21M4.9 19.1l1.6-1.6M17.5 6.5l1.6-1.6" />
    </svg>
  );
}

function ActionsIcon({ className }: IconProps) {
  return (
    <svg {...strokeProps(className)}>
      <path d="M8 6h13M8 12h13M8 18h8" />
      <path d="M4 6h.01M4 12h.01M4 18h.01" />
    </svg>
  );
}

function FormMapIcon({ className }: IconProps) {
  return (
    <svg {...strokeProps(className)}>
      <rect x="4" y="4" width="7" height="16" rx="1.2" />
      <rect x="13" y="4" width="7" height="16" rx="1.2" />
      <path d="M7 8h1M7 12h1M16 8h1M16 12h1M16 16h1" />
    </svg>
  );
}

function AuditIcon({ className }: IconProps) {
  return (
    <svg {...strokeProps(className)}>
      <path d="M9 5H5a1 1 0 0 0-1 1v13a1 1 0 0 0 1 1h14a1 1 0 0 0 1-1V6a1 1 0 0 0-1-1h-4" />
      <path d="M9 5a2 2 0 0 1 2-2h2a2 2 0 0 1 2 2v1H9V5z" />
      <path d="m9 14 2 2 4-4" />
    </svg>
  );
}
