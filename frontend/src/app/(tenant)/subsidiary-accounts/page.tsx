import { Suspense } from "react";
import { SubsidiaryAccountsListingPage } from "@/components/coa/subsidiary-accounts-listing-page";

export default function Page() {
  return (
    <Suspense fallback={null}>
      <SubsidiaryAccountsListingPage />
    </Suspense>
  );
}
