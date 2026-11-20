import { Suspense } from "react";
import { UsersListingPage } from "@/components/permission-management/users-listing-page";

export default function Page() {
  return (
    <Suspense fallback={null}>
      <UsersListingPage />
    </Suspense>
  );
}
