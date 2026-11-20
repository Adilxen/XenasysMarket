import { chromium } from "playwright";
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const OUT = path.join(__dirname, "..", ".qa-shots");
fs.mkdirSync(OUT, { recursive: true });

async function main() {
  const findings = [];
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({
    viewport: { width: 1440, height: 900 },
    deviceScaleFactor: 1,
  });
  const page = await context.newPage();
  const consoleErrors = [];
  const pageErrors = [];
  page.on("console", (msg) => {
    if (msg.type() === "error") consoleErrors.push(msg.text());
  });
  page.on("pageerror", (err) => pageErrors.push(String(err)));

  // --- LOGIN ---
  await page.goto("http://localhost:3000/login", { waitUntil: "networkidle" });
  await page.waitForTimeout(800);
  await page.screenshot({ path: path.join(OUT, "01-login.png"), fullPage: true });

  const login = await page.evaluate(() => {
    const h1 = document.querySelector("h1");
    const form = document.querySelector("form");
    const brand = document.querySelector('aside[aria-label*="brand" i], aside');
    const iso = document.body.innerText.includes("ISO 27001");
    const ssl = document.body.innerText.includes("256-bit SSL");
    const companyBtn = Array.from(document.querySelectorAll("button")).find((b) =>
      /select your company|xenasys master/i.test(b.textContent || ""),
    );
    const inputs = Array.from(document.querySelectorAll("input")).map((i) => ({
      type: i.type,
      placeholder: i.placeholder,
      autocomplete: i.autocomplete,
    }));
    const formRect = form?.getBoundingClientRect();
    const h1Rect = h1?.getBoundingClientRect();
    return {
      title: h1?.textContent?.trim(),
      hasBrandAside: !!brand,
      brandVisible: brand ? getComputedStyle(brand).display !== "none" : false,
      iso,
      ssl,
      hasCompanyControl: !!companyBtn,
      inputs,
      formWidth: formRect?.width ?? 0,
      h1Font: h1 ? getComputedStyle(h1).fontFamily : null,
      h1Size: h1 ? getComputedStyle(h1).fontSize : null,
      bodyBg: getComputedStyle(document.body).backgroundColor,
      rightBg: form?.parentElement
        ? getComputedStyle(form.parentElement).backgroundColor
        : null,
    };
  });

  if (login.iso) findings.push({ sev: "high", area: "login", issue: "ISO 27001 badge still visible" });
  if (login.ssl) findings.push({ sev: "high", area: "login", issue: "256-bit SSL badge still visible" });
  if (!login.hasCompanyControl)
    findings.push({ sev: "high", area: "login", issue: "Company select control not found" });
  if (!login.inputs.some((i) => i.type === "password" || i.placeholder.includes("•")))
    findings.push({ sev: "med", area: "login", issue: "Password input missing or not type=password initially" });
  if (!login.inputs.some((i) => /adil raza/i.test(i.placeholder || "")))
    findings.push({ sev: "low", area: "login", issue: "Username placeholder is not Adil Raza" });

  // password show/hide button
  const eye = page.getByTestId("toggle-password");
  if ((await eye.count()) === 0) {
    findings.push({ sev: "high", area: "login", issue: "Password show/hide button missing" });
  } else {
    // Prefer DOM click to validate handler (hit-testing can flake with overlapping input padding).
    await page.getByTestId("toggle-password").evaluate((el) => el.click());
    await page.waitForTimeout(200);
    const typeAfter = await page.getByTestId("password-input").getAttribute("type");
    const pressed = await page.getByTestId("toggle-password").getAttribute("aria-pressed");
    if (typeAfter !== "text")
      findings.push({
        sev: "high",
        area: "login",
        issue: `Show password did not switch type (got ${typeAfter}, aria-pressed=${pressed})`,
      });
    await page.getByTestId("toggle-password").evaluate((el) => el.click());
    await page.waitForTimeout(100);
  }

  // open company dropdown + pick first
  await page.getByRole("button", { name: /select your company|xenasys master/i }).first().click();
  await page.waitForTimeout(300);
  const companyOption = page.getByRole("button", { name: /xenasys master tenant/i });
  if ((await companyOption.count()) === 0) {
    findings.push({ sev: "high", area: "login", issue: "Company list empty / API companies not loading" });
  } else {
    await companyOption.first().click();
  }

  await page.getByPlaceholder("Adil Raza").fill("tenantadmin");
  await page.locator('input[autocomplete="current-password"]').fill("TenantAdmin@12345");
  await page.screenshot({ path: path.join(OUT, "02-login-filled.png"), fullPage: true });

  await page.getByRole("button", { name: /^login$/i }).click();
  await page.waitForTimeout(1500);

  const urlAfter = page.url();
  if (!urlAfter.includes("/dashboard")) {
    const errText = await page.locator('[role="alert"]').allTextContents().catch(() => []);
    findings.push({
      sev: "high",
      area: "login",
      issue: `Login did not reach dashboard (url=${urlAfter}) alerts=${JSON.stringify(errText)}`,
    });
    // try API diagnosis still continue to dashboard via token injection? skip
  } else {
    await page.waitForTimeout(500);
    await page.screenshot({ path: path.join(OUT, "03-dashboard.png"), fullPage: true });

    const dash = await page.evaluate(() => {
      const aside = document.querySelector("aside");
      const header = document.querySelector("header");
      const logoutBtn = Array.from(document.querySelectorAll("button")).some((b) =>
        /^logout$/i.test((b.textContent || "").trim()),
      );
      const signOutVisible = Array.from(document.querySelectorAll("button")).some((b) =>
        /sign out/i.test(b.textContent || ""),
      );
      const navLinks = Array.from(document.querySelectorAll("aside a")).map((a) => ({
        href: a.getAttribute("href"),
        text: a.textContent?.trim(),
      }));
      const footerImg = document.querySelector('aside img[alt*="XENASYS Development" i], aside img[src*="footerlogo"]');
      const search = document.querySelector('input[type="search"]');
      const searchRect = search?.getBoundingClientRect();
      const headerRect = header?.getBoundingClientRect();
      const brandText = aside?.innerText || "";
      const profileName = header?.innerText || "";
      return {
        asideWidth: aside?.getBoundingClientRect().width ?? 0,
        headerHeight: headerRect?.height ?? 0,
        logoutBtnInline: logoutBtn,
        signOutVisible,
        navCount: navLinks.length,
        navLinks,
        hasDashboard: navLinks.some((n) => n.href === "/dashboard"),
        hasProducts: navLinks.some((n) => n.href === "/products"),
        hasFooterLogo: !!footerImg,
        footerSrc: footerImg?.getAttribute("src"),
        searchPresent: !!search,
        searchCenterDelta: searchRect && headerRect
          ? Math.abs(searchRect.left + searchRect.width / 2 - (headerRect.left + headerRect.width / 2))
          : null,
        brandTextSample: brandText.slice(0, 200),
        profileTextSample: profileName.slice(0, 200),
        hasFakeDisplayAlias: /Demo Admin|Platform Admin/i.test(profileName),
        hasIso: /ISO 27001/i.test(document.body.innerText),
      };
    });

    if (dash.asideWidth < 240 || dash.asideWidth > 280)
      findings.push({ sev: "med", area: "dashboard", issue: `Sidebar width off mock (~260px): ${dash.asideWidth}` });
    if (dash.logoutBtnInline)
      findings.push({ sev: "med", area: "dashboard", issue: "Inline Logout button still visible (should be profile menu only)" });
    if (!dash.hasFooterLogo)
      findings.push({ sev: "high", area: "dashboard", issue: "footerlogo.png missing in sidebar footer" });
    if (!dash.searchPresent)
      findings.push({ sev: "high", area: "dashboard", issue: "Header search missing" });
    if (dash.searchCenterDelta != null && dash.searchCenterDelta > 120)
      findings.push({
        sev: "med",
        area: "dashboard",
        issue: `Search not visually centered (delta=${Math.round(dash.searchCenterDelta)}px)`,
      });
    if (dash.navCount < 12)
      findings.push({ sev: "med", area: "dashboard", issue: `Nav link count low (${dash.navCount}) vs Main screen IA` });
    if (dash.hasFakeDisplayAlias)
      findings.push({
        sev: "med",
        area: "dashboard",
        issue: "Profile still shows hardcoded alias (Platform Admin / Demo Admin) instead of live UserName",
      });
    if (!/XENASYS/i.test(dash.brandTextSample))
      findings.push({ sev: "high", area: "dashboard", issue: "Sidebar brand text XENASYS missing" });
    if (/Tenant Admin[\s\S]*Tenant Admin/.test(dash.profileTextSample))
      findings.push({
        sev: "med",
        area: "dashboard",
        issue: "Profile name and role both show Tenant Admin (duplicate)",
      });
    if (!dash.navLinks?.some((n) => n.href === "/company-profile") ||
        !dash.navLinks?.some((n) => n.href === "/system-settings"))
      findings.push({
        sev: "med",
        area: "dashboard",
        issue: "SETTINGS routes missing from sidebar nav links",
      });

    // open profile menu
    await page.locator("header button[aria-haspopup='menu']").click();
    await page.waitForTimeout(200);
    await page.screenshot({ path: path.join(OUT, "04-dashboard-profile-menu.png") });
    const hasSignOut = await page.getByRole("menuitem", { name: /sign out/i }).count();
    if (!hasSignOut) findings.push({ sev: "high", area: "dashboard", issue: "Sign out menu item missing" });

    // navigate products
    await page.getByRole("link", { name: /products/i }).first().click();
    await page.waitForTimeout(700);
    await page.screenshot({ path: path.join(OUT, "05-products.png"), fullPage: true });
    if (!page.url().includes("/products"))
      findings.push({ sev: "high", area: "nav", issue: "Products nav did not route to /products" });
  }

  if (pageErrors.length)
    findings.push({ sev: "high", area: "runtime", issue: `pageerror: ${pageErrors.slice(0, 3).join(" | ")}` });
  if (consoleErrors.length)
    findings.push({
      sev: "med",
      area: "console",
      issue: `console errors: ${consoleErrors.slice(0, 5).join(" | ")}`,
    });

  const report = {
    loginSnapshot: login,
    consoleErrors,
    pageErrors,
    findings,
    shots: fs.readdirSync(OUT).filter((f) => f.endsWith(".png")),
  };
  fs.writeFileSync(path.join(OUT, "report.json"), JSON.stringify(report, null, 2));
  console.log(JSON.stringify(report, null, 2));
  await browser.close();
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
