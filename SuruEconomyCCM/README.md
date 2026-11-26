# SuruEconomy — sunucu tarafı elmas koruması (Unity Cloud Code)

Oyun her elmas kazancını, harcamasını ve satın almasını bir deftere yazar (`Assets/Scripts/Core/Meta/GemLedger.cs`)
ve bu modüle gönderir (`SuruEconomy.Sync`). Sunucu:

- gerçek bakiyeyi Cloud Save **Protected** alanında tutar (oyuncu okuyabilir, yalnızca sunucu yazabilir),
- kazançlara günlük ve ömür boyu sınır uygular (`EconomyRules.Daily` / `Lifetime`),
- bakiye yetmeyen harcamayı reddeder,
- satın alma makbuzunu doğrular: Google Play imzası, Apple verifyReceipt; aynı makbuz ikinci kez elmas vermez,
- Editor (sahte) satın almaları yalnızca `production` dışındaki ortamlarda kabul eder.

## Kurulum (bir kez)
1. Unity projesini Unity Cloud projesine bağlayın: **Edit > Project Settings > Services**.
2. **.NET 8 SDK** kurun (Microsoft'un sitesinden "Download .NET 8.0 SDK", Windows x64). Unity, modülü yüklerken bununla derler.
3. Unity'de **Services > Deployment** penceresini açın, `SuruEconomy` modülünü seçip **Deploy** deyin.

## Mağazalar eklenince
`SuruEconomy/StoreConfig.cs` içine Android paket adı, iOS Bundle ID ve Google Play lisans anahtarını yazıp yeniden Deploy edin.
Anahtar girilmeden önce Google satın almaları ve Samsung/Amazon/Huawei satın almaları günde en çok 3 kez, imzasız kabul edilir.

## Test
`powershell -ExecutionPolicy Bypass -File Tools\economy\run_tests.ps1` — .NET SDK gerekmez, Unity'nin derleyicisini kullanır.
Oyun içi bağlantı testi: Unity menüsü **Sürü Savaşı > Test > Elmas defteri**.
