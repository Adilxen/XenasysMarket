import { ModulePlaceholder } from "@/components/layout/module-placeholder";

/** Platform knobs deferred — tenant Settings hub keeps a labeled placeholder. */
export default function Page() {
  return (
    <ModulePlaceholder
      title="System Settings"
      description="Platform-level switches will land with Super Admin tooling. Tenant company and RBAC settings are under the other Settings tabs."
    />
  );
}
