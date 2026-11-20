import { Suspense } from "react";
import { StockListingPage } from "@/components/stock/stock-listing-page";

export default function Page() {
  return (
    <Suspense fallback={<div className="p-4 text-sm text-[#6B7280]">Loading stock…</div>}>
      <StockListingPage />
    </Suspense>
  );
}
