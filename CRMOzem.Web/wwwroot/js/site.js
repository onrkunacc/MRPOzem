// Para birimi seçildiğinde kuru otomatik doldur (EK GİDER modali)
const kurDefaults = { TRY: 1, USD: 50, EUR: 60 };

const paraBirimiSel = document.getElementById('e_paraBirimi');
if (paraBirimiSel) {
    paraBirimiSel.addEventListener('change', () => {
        const k = document.getElementById('e_kur');
        if (k) k.value = kurDefaults[paraBirimiSel.value] ?? 1;
    });
}
