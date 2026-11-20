"use client";

import { memo, useState, type ChangeEvent } from "react";
import { useDebouncedCallback } from "@/hooks/use-debounced-callback";
import { fieldErrorClass, inputClass } from "@/lib/forms/field-styles";

type IsolatedTextFieldProps = {
  label: string;
  name: string;
  defaultValue?: string;
  type?: string;
  placeholder?: string;
  error?: string;
  /** Debounced commit to parent — parent state updates without every keystroke. */
  onCommit: (value: string) => void;
  debounceMs?: number;
  onBlurValidate?: (value: string) => void;
};

/**
 * Encapsulated field: local state absorbs keypresses; parent only receives
 * debounced commits — prevents full-form / page re-renders on typing.
 * Error chrome uses BRD #DC2626; focus ring uses brand accent #2563EB.
 */
export const IsolatedTextField = memo(function IsolatedTextField({
  label,
  name,
  defaultValue = "",
  type = "text",
  placeholder,
  error,
  onCommit,
  debounceMs = 250,
  onBlurValidate,
}: IsolatedTextFieldProps) {
  const [value, setValue] = useState(defaultValue);
  const commit = useDebouncedCallback((next: string) => onCommit(next), debounceMs);

  function handleChange(e: ChangeEvent<HTMLInputElement>) {
    const next = e.target.value;
    setValue(next);
    commit(next);
  }

  return (
    <label className="block space-y-1.5">
      <span className="text-[12.5px] font-semibold text-[#374151]">{label}</span>
      <input
        name={name}
        type={type}
        value={value}
        placeholder={placeholder}
        onChange={handleChange}
        onBlur={() => onBlurValidate?.(value)}
        className={inputClass(Boolean(error))}
        aria-invalid={Boolean(error)}
      />
      {error ? <span className={fieldErrorClass()}>{error}</span> : null}
    </label>
  );
});
