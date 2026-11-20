import { Suspense } from "react";
import { CoaTreePage } from "@/components/coa/coa-tree-page";

export default function Page() {
  return (
    <Suspense fallback={null}>
      <CoaTreePage />
    </Suspense>
  );
}
