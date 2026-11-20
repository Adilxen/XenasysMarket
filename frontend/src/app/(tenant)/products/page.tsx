import { Suspense } from "react";
import { ProductListingPage } from "@/components/products/product-listing-page";
import { TableSkeleton } from "@/components/ui/skeleton";

/** RSC shell — listing mounts behind Suspense; drawer CRUD is client-side. */
export default function ProductsPage() {
  return (
    <Suspense fallback={<TableSkeleton rows={10} columns={5} />}>
      <ProductListingPage />
    </Suspense>
  );
}
