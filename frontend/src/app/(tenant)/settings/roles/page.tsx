import { Suspense } from "react";
import { RolesListingPage } from "@/components/permission-management/roles-listing-page";

export default function Page() {
  return (
    <Suspense fallback={null}>
      <RolesListingPage />
    </Suspense>
  );
}
