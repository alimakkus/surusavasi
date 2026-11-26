# Otomatik ekran görüntüsü: projenin bir kopyasından Windows sürümü derler, örnek oyuncuyla ekranları açıp PNG kaydeder.
# Asıl projeye ve Unity'deki kayda dokunmaz (kopya %TEMP%\suru_shot altında). İlk çalıştırma uzun sürer (içe aktarma).
# Kullanım: powershell -ExecutionPolicy Bypass -File Tools\autoshot\run_autoshot.ps1
$u = "C:\Program Files\Unity\Hub\Editor\2022.3.62f3\Editor\Unity.exe"
$root = Split-Path (Split-Path $PSScriptRoot)
$w = "$env:TEMP\suru_shot"; $proj = "$w\proj"; $out = "$w\shots"
New-Item -ItemType Directory -Force $proj, $out | Out-Null
foreach ($d in "Assets", "Packages", "ProjectSettings") { robocopy "$root\$d" "$proj\$d" /MIR /NFL /NDL /NJH /NJS /NP | Out-Null }
Remove-Item "$out\*.png" -ErrorAction SilentlyContinue
$b = Start-Process $u -ArgumentList @('-batchmode', '-projectPath', "`"$proj`"", '-buildTarget', 'Win64', '-executeMethod', 'AutoShotBuild.Build', '-out', "`"$w\build\Suru.exe`"", '-logFile', "`"$w\build.log`"") -PassThru
$b.WaitForExit()
if ($b.ExitCode -ne 0) { "Derleme başarısız, bkz. $w\build.log"; Select-String -Path "$w\build.log" -Pattern "error CS|Error building" | Select-Object -First 10 | % { $_.Line }; exit 1 }
$g = Start-Process "$w\build\Suru.exe" -ArgumentList @('-autoshot', "`"$out`"", '-screen-fullscreen', '0', '-screen-width', '450', '-screen-height', '800', '-logFile', "`"$w\player.log`"") -PassThru
if (-not $g.WaitForExit(120000)) { $g.Kill(); "Oyun zaman aşımı" }
Get-ChildItem $out -Filter *.png | % { $_.FullName }
