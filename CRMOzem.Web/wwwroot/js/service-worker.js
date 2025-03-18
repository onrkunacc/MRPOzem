self.addEventListener("install", event => {
    console.log("Service Worker Yüklendi");

    event.waitUntil(
        caches.open("ozem-cache").then(cache => {
            return cache.addAll([
                "/",
                "/css/custom.css",
                "/tinydash-master/light/css/app-light.css",
                "/tinydash-master/light/js/apps.js"
            ]);
        })
    );
});

self.addEventListener("activate", event => {
    console.log("Service Worker Aktif Edildi");
    event.waitUntil(clients.claim());  
});

self.addEventListener("fetch", event => {
    event.respondWith(
        caches.match(event.request).then(response => {
            return response || fetch(event.request);
        })
    );
});
