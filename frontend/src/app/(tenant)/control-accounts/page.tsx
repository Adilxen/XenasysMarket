import { Suspense } from "react";
import { ControlAccountsListingPage } from "@/components/coa/control-accounts-listing-page";

export default function Page() {
  return (
    <Suspense fallback={null}>
      <ControlAccountsListingPage />
    </Suspense>
  );
}
