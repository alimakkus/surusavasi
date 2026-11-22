# Unity'siz denge simülasyonu: Core kodunu derler ve BalanceModel'i çalıştırır.
$u = "C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Data"
$w = "$env:TEMP\simrun"; New-Item -ItemType Directory -Force $w | Out-Null
$root = Split-Path (Split-Path $PSScriptRoot)
Copy-Item "$PSScriptRoot\SimMain.cs" "$w\P.cs" -Force
$files = Get-ChildItem "$root\Assets\Scripts\Core" -Recurse -Filter *.cs | % { '"' + $_.FullName + '"' }
$outp = ('-out:"' + $w + '\sim.dll"'); $pf = ('"' + $w + '\P.cs"')
$rsp = @('-nologo', '-target:exe', '-langversion:9', '-nowarn:CS0618,CS0649,CS0414,CS0168,CS0219', '-nostdlib',
  ('-r:"' + $u + '\NetStandard\ref\2.1.0\netstandard.dll"'),
  ('-r:"' + $u + '\Managed\UnityEngine\UnityEngine.CoreModule.dll"'),
  ('-r:"' + $u + '\Managed\UnityEngine\UnityEngine.JSONSerializeModule.dll"'), $outp, $pf) + $files
$rsp | Set-Content "$w\b.rsp"
& "$u\NetCoreRuntime\dotnet.exe" "$u\DotNetSdkRoslyn\csc.dll" "@$w\b.rsp"
if ($LASTEXITCODE -ne 0) { exit 1 }
Copy-Item "$u\Managed\UnityEngine\UnityEngine.CoreModule.dll", "$u\Managed\UnityEngine\UnityEngine.JSONSerializeModule.dll" $w -Force
$rt = (Get-ChildItem "$u\NetCoreRuntime\shared\Microsoft.NETCore.App" | Select-Object -First 1).Name
'{"runtimeOptions":{"tfm":"net6.0","framework":{"name":"Microsoft.NETCore.App","version":"' + $rt + '"}}}' | Set-Content "$w\sim.runtimeconfig.json"
Push-Location $w; & "$u\NetCoreRuntime\dotnet.exe" sim.dll; Pop-Location
