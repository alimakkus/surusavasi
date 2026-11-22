# Unity'siz denge simülasyonu

Unity açık değilken (ya da hızlı deneme için) `Assets/Scripts/Core` kodunu Unity'nin kendi derleyicisi ve
.NET çalışma ortamıyla derleyip `BalanceModel`'i çalıştırır. ~20 sn sürer.

Windows PowerShell:

```powershell
pwsh Tools/balance/run_sim.ps1
```

Çıktı: oyuncu tiplerine göre 100. bölüme ulaşma süresi ve %TEMP%\simrun\ortalama.csv.
