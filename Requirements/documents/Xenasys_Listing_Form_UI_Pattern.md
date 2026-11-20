# Xenasys Listing & Form UI Pattern (SSoT)

**Source screens (locked):**  
- Category Listing — `Requirements/Screens` / workspace assets `Category_Listing_Screen-*.png`  
- Category Form (right drawer) — `Category_Form_Screen-*.png`  

**Related:** `Xenasys_UI_Validation_Plan.md` (RHF+Zod / `#DC2626`) · `frontend/src/app/globals.css` · `frontend/src/lib/forms/field-styles.ts`  

**Rule:** Har nayi **listing** aur **form** isi pattern pe banni hai. Emerald/green accents **banned**. Primary accent = brand blue `#2563EB`.

---

## 1. Shell (dono screens common)

| Element | Spec |
|---------|------|
| Sidebar | Navy `#0B1B33` / `#0B1426` |
| Active nav | Solid accent blue `#2563EB`, white label; optional left indicator bar |
| Canvas | Light gray `#F5F7FB` / `#F8F9FA` |
| Top header | White; centered search (pill / soft gray fill); bell + blue avatar |
| Brand orange | Logo mark only — **not** for primary buttons |

---

## 2. Listing page pattern (Category Listing)

### 2.1 Page chrome
1. **Breadcrumb** — small muted gray (`Catalog / Categories`)  
2. **Title** — large bold ink `#1A2238` (`Category Listing`)  
3. **Subtitle** — one short muted line under title  
4. **Header actions (right):**  
   - **Secondary:** Export — white + gray border + dark text + icon  
   - **Primary:** `+ Add …` — solid `#2563EB`, white text, leading `+`

### 2.2 Data card
- White card, rounded (`rounded-xl` / ~12–16px), light border `#E8ECF2`, soft shadow  
- **Toolbar row:** local search + status filter (soft gray border / soft fill inputs)  
- **No** floating widgets outside the card

### 2.3 Table
| Part | Spec |
|------|------|
| Header row | Soft gray bg `#F7F9FC`; muted column labels |
| Body | White rows; **horizontal** hairline dividers only — **no** vertical borders |
| Primary cell | Bold dark name |
| Secondary cell | Regular muted gray (description, dates) |
| Thumbnails | Small square, rounded, light border, white fill |
| Row actions | Icon-only: **View** blue eye · **Edit** slate/blue pencil · **Delete** soft red trash |
| Footer | Left: `Showing X to Y of Z …` · Right: pagination; **active page** = blue square |

### 2.4 Forbidden on listings
- Green buttons / green borders / emerald focus  
- Dense card stacks in the hero area  
- Vertical table rules / heavy shadows  

---

## 3. Form pattern (Add Category — right drawer)

### 3.1 Interaction model
- Create/Edit opens a **right slide-over drawer** (~1/3 viewport), **not** a full-page form by default  
- Backdrop: semi-transparent dark overlay over listing  
- Drawer: white panel; header title + gray **X** close  

### 3.2 Fields
| Part | Spec |
|------|------|
| Label | Bold dark; required = red `*` (`#DC2626`) |
| Input / textarea | White fill; light gray border `#D1D5DB` / `#E2E8F0`; ~6–8px radius |
| Placeholder | Light gray (optional italic) |
| Focus | Brand blue border + soft blue ring (`rgba(37,99,235,0.12)`) — **never green** |
| Invalid | Border + message `#DC2626` (UI Validation Plan) |

### 3.3 Image upload
- Dashed border box; light blue-tint background `#EFF6FF`  
- Center: cloud icon + “Upload … Image” + helper lines (size/type)  
- Click / drag-drop affordance  

### 3.4 Drawer footer
- Top border separator  
- **Cancel** — white + gray border + dark text  
- **Save …** — solid `#2563EB` + white text (primary)  
- Buttons right-aligned  

### 3.5 Forbidden on forms
- Full-width green Save buttons  
- Emerald focus rings  
- Unstructured floating error text (use Alert wrapper)  

---

## 4. Token map (implement in code)

| Token | Value | Use |
|-------|--------|-----|
| `--x-navy` | `#0B1B33` | Sidebar |
| `--x-ink` | `#1A2238` | Titles |
| `--x-muted` | `#6B7280` | Subtitles / secondary cells |
| `--x-border` | `#E8ECF2` | Cards / row dividers |
| `--x-accent` | `#2563EB` | Primary CTA, active nav, focus, view icon |
| `--x-accent-hover` | `#1D4ED8` | Primary hover |
| `--x-accent-soft` | `#EFF6FF` | Upload zone / soft fills |
| `--x-danger` | `#DC2626` | Validation + delete |

**Helpers:** `frontend/src/lib/forms/field-styles.ts`  
(`btnPrimaryClass`, `btnSecondaryClass`, `inputClass`, `panelClass`, `pageHeaderClass`, `drawer*`, `table*`)

---

## 5. Component inventory (target shared UI)

```text
frontend/src/components/ui/
  page-header.tsx       # breadcrumb + title + subtitle + actions
  data-card.tsx         # white listing card shell
  data-table.tsx        # header + rows contract
  side-drawer.tsx       # right slide-over + overlay
  alert.tsx             # structured errors (exists)
  file-dropzone.tsx     # dashed upload zone
```

Naye catalog modules (Categories, Brands, Products, Vendors…) **must** reuse these — copy-paste one-off emerald/green styles allow nahi.

---

## 6. Agent / developer checklist

- [ ] Listing = breadcrumb + title + Export secondary + Add primary  
- [ ] Table inside white card; horizontal rules only  
- [ ] Form = right drawer + Cancel/Save footer  
- [ ] Focus blue; errors `#DC2626`; no emerald  
- [ ] Upload = dashed soft-blue zone  
- [ ] Cross-check `Xenasys_UI_Validation_Plan.md` for RHF `onBlur`  

---

*This file is the visual SSoT for listing + form chrome across Xenasys Market Place.*
