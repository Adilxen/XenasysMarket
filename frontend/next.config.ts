import type { NextConfig } from "next";
import path from "node:path";
import { fileURLToPath } from "node:url";

const workspaceRoot = path.dirname(fileURLToPath(import.meta.url));

/**
 * T0.6 — Keep Turbopack rooted at this package (node_modules linker).
 * No custom webpack/turbopack aliases that would clash with repo path rules.
 */
const nextConfig: NextConfig = {
  turbopack: {
    root: workspaceRoot,
  },
};

export default nextConfig;
