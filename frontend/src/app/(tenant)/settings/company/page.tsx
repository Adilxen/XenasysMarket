import { Suspense } from "react";
import { CompanyListingPage } from "@/components/settings/company-listing-page";

export default function Page() {
  return (
    <Suspense fallback={null}>
      <CompanyListingPage />
    </Suspense>
  );
}
