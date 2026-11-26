# Sunucu elmas kurallarının (SuruEconomyCCM) Unity'siz testi: Unity'nin derleyicisi ve .NET çalışma zamanıyla.
# .NET SDK gerekmez. Kullanım: powershell -ExecutionPolicy Bypass -File Tools\economy\run_tests.ps1
$u = "C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Data"
$w = "$env:TEMP\ecotest"; New-Item -ItemType Directory -Force $w | Out-Null
$root = Split-Path (Split-Path $PSScriptRoot)
$fw = (Get-ChildItem "$u\NetCoreRuntime\shared\Microsoft.NETCore.App" | Select-Object -First 1)
$refs = Get-ChildItem $fw.FullName -Filter *.dll | ? { $_.Name -match '^(System|Microsoft|mscorlib|netstandard)' -and $_.Name -notmatch 'Native|VisualBasic' } | % { '-r:"' + $_.FullName + '"' }
$src = @("$root\SuruEconomyCCM\SuruEconomy\EconomyRules.cs", "$root\SuruEconomyCCM\SuruEconomy\Receipts.cs", "$root\SuruEconomyCCM\SuruEconomy\StoreConfig.cs", "$root\Assets\Scripts\Core\Meta\GemLedger.cs", "$PSScriptRoot\FlowTest.cs", "$PSScriptRoot\TestMain.cs") | % { '"' + $_ + '"' }
$rsp = @('-nologo', '-target:exe', '-langversion:9', '-nostdlib', '-nowarn:CS8632', ('-out:"' + $w + '\ecotest.dll"')) + $refs + $src
$rsp | Set-Content "$w\b.rsp"
& "$u\NetCoreRuntime\dotnet.exe" "$u\DotNetSdkRoslyn\csc.dll" "@$w\b.rsp"
if ($LASTEXITCODE -ne 0) { exit 1 }
'{"runtimeOptions":{"tfm":"net6.0","framework":{"name":"Microsoft.NETCore.App","version":"' + $fw.Name + '"}}}' | Set-Content "$w\ecotest.runtimeconfig.json"
& "$u\NetCoreRuntime\dotnet.exe" "$w\ecotest.dll"
exit $LASTEXITCODE
