/** Mirrors Xenasys.Core.Contracts.Search DTOs for Global Search Center. */

export interface GlobalSearchHitDto {
  id: string;
  kind: string;
  label: string;
  description?: string | null;
  href: string;
}

export interface GlobalSearchGroupDto {
  key: string;
  label: string;
  hits: GlobalSearchHitDto[];
}

export interface GlobalSearchResponseDto {
  query: string;
  groups: GlobalSearchGroupDto[];
}
