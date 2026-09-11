// ── debug-console.js ────────────────────────────────────────────────────
// Console de debug visível no próprio celular, sem precisar de computador.
// Ativa: abre a URL com ?debug=1 uma vez (ex: .../pdv?debug=1). Fica ligado
// até você desligar (persiste em localStorage, sobrevive a navegação e recarga).
// Desativa: abre a URL com ?debug=0.
(function () {
    const params = new URLSearchParams(window.location.search);

    if (params.get('debug') === '1') localStorage.setItem('ttsoft_debug', '1');
    if (params.get('debug') === '0') localStorage.removeItem('ttsoft_debug');

    if (localStorage.getItem('ttsoft_debug') === '1') {
        const script = document.createElement('script');
        script.src = 'lib/eruda.min.js';
        script.onload = function () { window.eruda.init(); };
        document.body.appendChild(script);
    }
})();
