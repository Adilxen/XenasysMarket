"use client";

/**
 * ==========================================================================================
 * WHAT: Listing row Actions — View / Edit / Delete icons (permission-gated by caller).
 * WHY: Icons must not appear when the handler is missing or show* is false (M6 ClaimGate).
 * EXAMPLE: <ListingRowActions onView={…} showEdit={canUpdate} showDelete={canDelete} />
 * ==========================================================================================
 */

import { btnGhostClass } from "@/lib/forms/field-styles";

export function ListingRowActions({
  onView,
  onEdit,
  onDelete,
  viewTitle = "View",
  editTitle = "Edit",
  deleteTitle = "Delete",
  showView = true,
  showEdit = true,
  showDelete = true,
}: {
  onView?: () => void;
  onEdit?: () => void;
  onDelete?: () => void;
  viewTitle?: string;
  editTitle?: string;
  deleteTitle?: string;
  showView?: boolean;
  showEdit?: boolean;
  showDelete?: boolean;
}) {
  const viewVisible = Boolean(showView && onView);
  const editVisible = Boolean(showEdit && onEdit);
  const deleteVisible = Boolean(showDelete && onDelete);

  if (!viewVisible && !editVisible && !deleteVisible) {
    return <span className="text-xs text-[#94A3B8]">—</span>;
  }

  return (
    <div className="flex flex-nowrap items-center justify-center gap-0.5">
      {viewVisible ? (
        <button
          type="button"
          className={btnGhostClass("text-[#2563EB] hover:bg-[#EFF6FF]")}
          title={viewTitle}
          aria-label={viewTitle}
          onClick={onView}
        >
          <EyeIcon />
        </button>
      ) : null}
      {editVisible ? (
        <button
          type="button"
          className={btnGhostClass("text-[#64748B] hover:bg-[#F3F5F9]")}
          title={editTitle}
          aria-label={editTitle}
          onClick={onEdit}
        >
          <EditIcon />
        </button>
      ) : null}
      {deleteVisible ? (
        <button
          type="button"
          className={btnGhostClass("text-[#DC2626] hover:bg-[#FEF2F2]")}
          title={deleteTitle}
          aria-label={deleteTitle}
          onClick={onDelete}
        >
          <TrashIcon />
        </button>
      ) : null}
    </div>
  );
}

function EyeIcon() {
  return (
    <svg width="15" height="15" viewBox="0 0 24 24" fill="none" aria-hidden>
      <path
        d="M2 12s3.5-7 10-7 10 7 10 7-3.5 7-10 7S2 12 2 12z"
        stroke="currentColor"
        strokeWidth="1.8"
        strokeLinejoin="round"
      />
      <circle cx="12" cy="12" r="3" stroke="currentColor" strokeWidth="1.8" />
    </svg>
  );
}

function EditIcon() {
  return (
    <svg width="15" height="15" viewBox="0 0 24 24" fill="none" aria-hidden>
      <path
        d="M12 20h9M16.5 3.5a2.12 2.12 0 0 1 3 3L7 19l-4 1 1-4 12.5-12.5z"
        stroke="currentColor"
        strokeWidth="1.8"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}

function TrashIcon() {
  return (
    <svg width="15" height="15" viewBox="0 0 24 24" fill="none" aria-hidden>
      <path
        d="M3 6h18M8 6V4h8v2M19 6l-1 14H6L5 6"
        stroke="currentColor"
        strokeWidth="1.8"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}
