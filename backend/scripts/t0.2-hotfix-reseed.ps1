# T0.2 — Wipe bad seed + apply schema hotfix + re-seed via API boot
# Run from: backend/

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot\..

Write-Host "==> Applying EF migration T0_2_SeedHotfix..." -ForegroundColor Cyan
dotnet ef migrations add T0_2_SeedHotfix_SequentialIdsAndPlatformMembership `
  --project .\src\Xenasys.Persistence\Xenasys.Persistence.csproj `
  --startup-project .\src\Xenasys.Presentation\Xenasys.Presentation.csproj `
  --output-dir Migrations `
  --context ApplicationDbContext

if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "==> Wiping flawed seed rows (SQL)..." -ForegroundColor Cyan
sqlcmd -S "localhost\SQLEXPRESS" -d "XenasysMarketplace" -E -C -i ".\scripts\t0.2-wipe-bad-seed.sql"
if ($LASTEXITCODE -ne 0) {
  Write-Host "sqlcmd failed — run scripts\t0.2-wipe-bad-seed.sql manually in SSMS, then continue." -ForegroundColor Yellow
}

Write-Host "==> database update..." -ForegroundColor Cyan
dotnet ef database update `
  --project .\src\Xenasys.Persistence\Xenasys.Persistence.csproj `
  --startup-project .\src\Xenasys.Presentation\Xenasys.Presentation.csproj `
  --context ApplicationDbContext

if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "==> Boot API once to run DatabaseSeeder..." -ForegroundColor Cyan
$env:ASPNETCORE_ENVIRONMENT = "Development"
$p = Start-Process -FilePath "dotnet" -ArgumentList @(
  "run","--project",".\src\Xenasys.Presentation\Xenasys.Presentation.csproj",
  "--urls","http://127.0.0.1:5099"
) -PassThru -NoNewWindow -RedirectStandardOutput ".\scripts\t0.2-seed-out.txt" -RedirectStandardError ".\scripts\t0.2-seed-err.txt"

Start-Sleep -Seconds 15
if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force }

Write-Host "==> Seed log (tail):" -ForegroundColor Cyan
Get-Content ".\scripts\t0.2-seed-out.txt" -ErrorAction SilentlyContinue | Select-String -Pattern "Seed|initialization|error|fail" | Select-Object -Last 30

Write-Host "==> T0.2 complete." -ForegroundColor Green
Write-Host "Super Admin: superadmin / SuperAdmin@12345 (CompanyId NULL membership)"
Write-Host "Tenant Admin: tenantadmin / TenantAdmin@12345 (MASTER tenant)"
