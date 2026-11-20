import { Suspense } from "react";
import { MasterAccountsListingPage } from "@/components/coa/master-accounts-listing-page";

export default function Page() {
  return (
    <Suspense fallback={null}>
      <MasterAccountsListingPage />
    </Suspense>
  );
}
