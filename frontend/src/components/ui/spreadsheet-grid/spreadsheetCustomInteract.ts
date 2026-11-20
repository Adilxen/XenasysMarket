/** Dispatched on `[data-sg-custom-interact]` to force-open pickers (Enter / F2). */
export const SG_CUSTOM_INTERACT_ACTIVATE = 'sg-custom-interact-activate';

/**
 * Activate a customInteract cell UI (date / datetime picker).
 * Prefer a dedicated event over `.click()` so pickers can force-open instead of toggle.
 */
export function activateSpreadsheetCustomInteract(cell: HTMLTableCellElement): boolean {
  const trigger = cell.querySelector(
    '[data-sg-custom-interact], .sg-sheet-datetime-trigger'
  ) as HTMLElement | null;
  if (!trigger) return false;
  trigger.dispatchEvent(
    new CustomEvent(SG_CUSTOM_INTERACT_ACTIVATE, { bubbles: true, cancelable: true })
  );
  return true;
}
