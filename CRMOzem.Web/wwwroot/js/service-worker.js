

const CACHE_VERSION = "ozem-v3";
const STATIC_CACHE = `${CACHE_VERSION}-static`;

// Pre-cache'lenecek kritik statik dosyalar
const STATIC_ASSETS = [
    "/css/custom.css",
    "/tinydash-master/light/css/app-light.css",
    "/tinydash-master/light/js/apps.js",
    "/icons/icon-192x192.png",
    "/icons/icon-512x512.png"
];

// Bu path prefix'ler ASLA cache'lenmez (auth, API, dinamik veri)
const NEVER_CACHE_PREFIXES = [
    "/Account",
    "/account",
    "/Identity",
    "/identity",
    "/Logout",
    "/logout",
    "/api/",
    "/Api/"
];

// Bu path prefix'ler statik olarak cache'lenebilir
const STATIC_PREFIXES = [
    "/css",
    "/js",
    "/lib",
    "/tinydash-master",
    "/icons",
    "/screenshots",
    "/favicon",
    "/images"
];

// ============================================================================
// INSTALL — pre-cache ve hemen aktif ol
// ============================================================================
self.addEventListener("install", event => {
    console.log(`[SW ${CACHE_VERSION}] Yükleniyor...`);
    self.skipWaiting(); // Yeni versiyonu hemen aktif et
    event.waitUntil(
        caches.open(STATIC_CACHE).then(cache => {
            // Her bir asset'i tek tek dene — biri olmazsa diğerleri cache'lensin
            return Promise.all(
                STATIC_ASSETS.map(asset =>
                    cache.add(asset).catch(err => {
                        console.warn(`[SW] "${asset}" cache'lenemedi:`, err.message);
                    })
                )
            );
        })
    );
});

// ============================================================================
// ACTIVATE — eski cache'leri temizle
// ============================================================================
self.addEventListener("activate", event => {
    console.log(`[SW ${CACHE_VERSION}] Aktif ediliyor...`);
    event.waitUntil(
        Promise.all([
            // Bu versiyona ait OLMAYAN tüm cache'leri sil
            caches.keys().then(keys =>
                Promise.all(
                    keys
                        .filter(k => !k.startsWith(CACHE_VERSION))
                        .map(k => {
                            console.log(`[SW] Eski cache siliniyor: ${k}`);
                            return caches.delete(k);
                        })
                )
            ),
            // Mevcut sekmelerin kontrolünü hemen al
            clients.claim()
        ])
    );
});

// ============================================================================
// FETCH — stratejik caching
// ============================================================================
self.addEventListener("fetch", event => {
    const req = event.request;
    const url = new URL(req.url);

    // Yalnızca GET isteklerini ve aynı origin'i yakala
    if (req.method !== "GET" || url.origin !== self.location.origin) {
        return; // Browser default davranışı
    }

    const path = url.pathname;

    // -------------------------------------------------------------------
    // Strateji 1: ASLA CACHE'LEME (auth, API, logout, dinamik veri)
    // -------------------------------------------------------------------
    if (NEVER_CACHE_PREFIXES.some(prefix => path.startsWith(prefix))) {
        event.respondWith(
            fetch(req, { credentials: "include" }).catch(() => {
                // Offline + auth endpoint → login sayfasına yönlendir
                return new Response(
                    "<!DOCTYPE html><html><head><meta charset='utf-8'><title>Çevrimdışı</title></head>" +
                    "<body style='font-family:sans-serif;padding:40px;text-align:center'>" +
                    "<h2>Bağlantı yok</h2><p>İnternet bağlantınızı kontrol edin ve tekrar deneyin.</p>" +
                    "<p><a href='/Account/Login'>Tekrar dene</a></p></body></html>",
                    { headers: { "Content-Type": "text/html; charset=utf-8" }, status: 503 }
                );
            })
        );
        return;
    }

    // -------------------------------------------------------------------
    // Strateji 2: HTML / NAVIGATION → NETWORK FIRST
    // Auth kontrolü sunucuda yapılması lazım, cache'lenirse Access Denied
    // sayfası PWA'da takılı kalabilir. Önce ağa git, olmazsa cache'e düş.
    // -------------------------------------------------------------------
    const acceptHeader = req.headers.get("accept") || "";
    const isHtml = req.mode === "navigate" ||
                   req.destination === "document" ||
                   acceptHeader.includes("text/html");

    if (isHtml) {
        event.respondWith(
            fetch(req, { credentials: "include" })
                .then(response => {
                    // Sadece başarılı sayfaları cache'e yaz (fallback için)
                    // 401/403 ASLA cache'lenmez
                    if (response.ok && response.status < 400) {
                        const clone = response.clone();
                        caches.open(STATIC_CACHE).then(c => {
                            // Sadece ana sayfa fallback için
                            if (path === "/") c.put(req, clone);
                        }).catch(() => {});
                    }
                    return response;
                })
                .catch(() => {
                    // Ağ yoksa cache'den dön (sadece "/" için)
                    return caches.match(req).then(cached => {
                        if (cached) return cached;
                        // Hiçbir şey yoksa offline mesajı
                        return new Response(
                            "<!DOCTYPE html><html><head><meta charset='utf-8'><title>Çevrimdışı</title>" +
                            "<meta name='viewport' content='width=device-width,initial-scale=1'></head>" +
                            "<body style='font-family:sans-serif;padding:40px;text-align:center;background:#f2f5f7'>" +
                            "<h2 style='color:#152c3b'>Çevrimdışı</h2>" +
                            "<p style='color:#637786'>İnternet bağlantınızı kontrol edin.</p>" +
                            "<button onclick='location.reload()' style='padding:12px 24px;background:#087e83;color:#fff;border:0;border-radius:8px;font-size:14px;margin-top:16px;cursor:pointer'>Tekrar dene</button>" +
                            "</body></html>",
                            { headers: { "Content-Type": "text/html; charset=utf-8" }, status: 503 }
                        );
                    });
                })
        );
        return;
    }

    // -------------------------------------------------------------------
    // Strateji 3: Statik dosyalar → CACHE FIRST
    // -------------------------------------------------------------------
    const isStatic = STATIC_PREFIXES.some(prefix => path.startsWith(prefix));

    if (isStatic) {
        event.respondWith(
            caches.match(req).then(cached => {
                if (cached) return cached;
                return fetch(req).then(response => {
                    if (response.ok) {
                        const clone = response.clone();
                        caches.open(STATIC_CACHE).then(c => c.put(req, clone)).catch(() => {});
                    }
                    return response;
                }).catch(() => {
                    // Statik dosya ağdan da gelmezse yapacak bir şey yok
                    return new Response("", { status: 504 });
                });
            })
        );
        return;
    }

    // -------------------------------------------------------------------
    // Strateji 4: Diğer her şey → NETWORK ONLY (cache'leme)
    // -------------------------------------------------------------------
    event.respondWith(fetch(req, { credentials: "include" }));
});

// ============================================================================
// MESSAGE HANDLER — sayfadan "sıfırla" komutu gelirse
// ============================================================================
// AccessDenied.cshtml'deki pwaRecover() fonksiyonu için destek
self.addEventListener("message", event => {
    if (!event.data) return;

    if (event.data.type === "SKIP_WAITING") {
        self.skipWaiting();
    }

    if (event.data.type === "CLEAR_CACHE") {
        event.waitUntil(
            caches.keys().then(keys =>
                Promise.all(keys.map(k => caches.delete(k)))
            )
        );
    }
});