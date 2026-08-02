# Builds the WHD Client MSI installer.
# Publishes the WPF app self-contained (no .NET runtime needed on target machines),
# then packages the output into installer\bin\Release\WHDClient-Setup.msi via WiX.
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

Write-Host '==> Publishing WHDClient (self-contained, single file)...'
dotnet publish "$root\src\WHDClient\WHDClient.csproj" `
    -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -o "$PSScriptRoot\publish"
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

Remove-Item "$PSScriptRoot\publish\*.pdb" -ErrorAction SilentlyContinue

Write-Host '==> Building MSI...'
dotnet build "$PSScriptRoot\WHDClient.Installer.wixproj" -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$msi = Get-ChildItem "$PSScriptRoot\bin\Release\*.msi" | Select-Object -First 1
Write-Host "==> Done: $($msi.FullName)"
