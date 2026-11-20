"use client";

import { memo } from "react";
import { useToggleProductActive } from "@/hooks/use-toggle-product-active";

type Props = {
  productId: number;
  isActive: boolean;
};

/** Isolated toggle — mutation state stays local; parent list does not re-render tree-wide. */
export const ProductActiveToggle = memo(function ProductActiveToggle({
  productId,
  isActive,
}: Props) {
  const { mutate, isPending } = useToggleProductActive();

  return (
    <button
      type="button"
      disabled={isPending}
      onClick={() => mutate({ id: productId, isActive: !isActive })}
      className={
        isActive
          ? "font-medium text-[#2563EB] hover:underline disabled:opacity-50"
          : "text-[#6B7280] hover:underline disabled:opacity-50"
      }
      aria-pressed={isActive}
    >
      {isActive ? "Active" : "Inactive"}
    </button>
  );
});
