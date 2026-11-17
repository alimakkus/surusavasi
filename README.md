# Sürü Savaşı

X-Clash benzeri zombi strateji oyunu (Android / iOS). Nihai motor: Unity.

- `prototype/index.html`: tek dosyalık HTML/JS prototip (oyun mantığı ve denge değerlerinin referansı)
- `Assets/Scripts/Core/`: Unity C# iskeleti (denge formülleri, bina verileri, kayıt sistemi)

Prototipi açmak için `prototype/index.html` dosyasını tarayıcıda aç.

## Altyapı (Assets/Scripts/Services)

- `GameServices`: tüm servislere tek giriş (kayıt, cüzdan, mağaza, reklam, analiz, onay).
- `IStore`, `IAdService`, `ISaveService`, `IAnalytics`, `IConsent`: her mağaza bunları uygular.
- `Platform/`: Editor için sahte mağaza ve reklam; Google Play, App Store, Samsung, Amazon, Huawei için yer tutucu sınıflar.
- Hedef mağaza, Player Settings → Scripting Define Symbols ile seçilir: `STORE_GOOGLE`, `STORE_APPLE`, `STORE_SAMSUNG`, `STORE_AMAZON`, `STORE_HUAWEI`.
- `Catalog`: elmas paketleri, başlangıç paketi, aylık kart, reklamsız paket. `Shop`: satın alma ve ödüllü reklamın oyun tarafı.
