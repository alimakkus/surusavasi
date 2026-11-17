# Sprite üretici

`Assets/Resources/Art` içindeki PNG'ler prototipin (prototype/index.html) kendi çizim kodundan üretilir.

1. `prototype/index.html` içinde `toTitle();` satırından sonra gelen `requestAnimationFrame(loop);` yerine `inject.js` içeriğini koy, `art.html` olarak kaydet.
2. `node artgen.js <art.html klasörü> <çıktı klasörü>` (Playwright gerekir).
3. Çıkan PNG'leri `Assets/Resources/Art` içine kopyala.

Sayfa düzeni `Assets/Scripts/Game/Art.cs` ile aynı olmalı.
