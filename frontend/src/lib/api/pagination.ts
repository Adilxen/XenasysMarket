import type { EntityId, KeysetPageRequest, KeysetPageResult } from "@/types/api";
import { apiGet } from "@/lib/api/client";
import { appendColumnFilters } from "@/hooks/use-server-column-filters";

/**
 * Keyset (cursor) pagination helper — mirrors backend KeysetPageRequest.
 * Prefer this over OFFSET/pageNumber for large product & ledger indexes.
 */
export function buildKeysetQuery(request: KeysetPageRequest = {}): string {
  const params = new URLSearchParams();
  if (request.afterId != null) {
    params.set("afterId", String(request.afterId));
  }
  params.set("limit", String(request.limit ?? 50));
  if (request.includeInactive) params.set("includeInactive", "true");
  if (request.shape) params.set("shape", request.shape);
  if (request.companyId != null && Number(request.companyId) > 0) {
    params.set("companyId", String(request.companyId));
  }
  if (request.lowStockOnly) {
    params.set("lowStockOnly", "true");
  }
  appendColumnFilters(params, request.filters);
  const qs = params.toString();
  return qs ? `?${qs}` : "";
}

export async function fetchKeysetPage<T>(
  path: string,
  request: KeysetPageRequest = {},
): Promise<KeysetPageResult<T>> {
  return apiGet<KeysetPageResult<T>>(`${path}${buildKeysetQuery(request)}`);
}

/** Accumulate pages until exhausted or maxPages — for export jobs, not UI hot path. */
export async function* iterateKeysetPages<T>(
  path: string,
  pageSize = 50,
  maxPages = 100,
): AsyncGenerator<T[], void, undefined> {
  let afterId: EntityId | null | undefined = undefined;
  let pages = 0;

  while (pages < maxPages) {
    const page: KeysetPageResult<T> = await fetchKeysetPage<T>(path, {
      afterId,
      limit: pageSize,
    });
    yield page.items;
    pages += 1;
    if (!page.hasMore || !page.nextCursor) break;
    afterId = page.nextCursor;
  }
}
