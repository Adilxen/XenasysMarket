# Xenasys — EF Core local SQL Server bootstrap
# Run from: backend/
# Note: API host project is Xenasys.Presentation (not Xenasys.WebApi)

$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

Write-Host "==> Ensuring dotnet-ef tool..." -ForegroundColor Cyan
dotnet tool update --global dotnet-ef | Out-Null
if ($LASTEXITCODE -ne 0) {
  dotnet tool install --global dotnet-ef
}

Write-Host "==> Building solution..." -ForegroundColor Cyan
dotnet build .\Xenasys.slnx --nologo
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "==> Adding migration InitialCreate..." -ForegroundColor Cyan
dotnet ef migrations add InitialCreate `
  --project .\src\Xenasys.Persistence\Xenasys.Persistence.csproj `
  --startup-project .\src\Xenasys.Presentation\Xenasys.Presentation.csproj `
  --output-dir Migrations `
  --context ApplicationDbContext

if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "==> Applying database update..." -ForegroundColor Cyan
dotnet ef database update `
  --project .\src\Xenasys.Persistence\Xenasys.Persistence.csproj `
  --startup-project .\src\Xenasys.Presentation\Xenasys.Presentation.csproj `
  --context ApplicationDbContext

if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Write-Host "==> Done. Database XenasysMarketplace should be ready on localhost\SQLEXPRESS." -ForegroundColor Green
