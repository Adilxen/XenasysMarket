import type { FieldValues, Path, UseFormSetError } from "react-hook-form";

/**
 * ==========================================================================================
 * WHAT: Maps ASP.NET 422 field-error payloads onto React Hook Form setError paths.
 * WHY: Server validation (e.g. company_id INT) must appear as RHF field messages in #DC2626.
 * EXAMPLE: { errors: { company_id: ["Invalid company."] } } → setError("company_id", { message }).
 * ==========================================================================================
 */
export type ApiFieldErrors = {
  errors?: Record<string, string[]>;
  error?: { fields?: Record<string, string[]> };
};

export function mapApiErrorsToForm<T extends FieldValues>(
  body: ApiFieldErrors | undefined,
  setError: UseFormSetError<T>,
): void {
  const fields = body?.error?.fields ?? body?.errors ?? {};
  for (const [key, messages] of Object.entries(fields)) {
    const message = messages?.[0];
    if (!message) continue;
    setError(key as Path<T>, { type: "server", message });
  }
}
