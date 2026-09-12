// ── pdv-storage.js ──────────────────────────────────────────────────────
// Achado (11/09) — carrinho do PDV Web se perdia inteiro ao navegar pra
// outra página (Produtos, Vendas) e voltar, já que o componente Blazor é
// recriado do zero. sessionStorage sobrevive à navegação dentro da mesma
// aba/sessão, mas limpa sozinho se a aba fechar — não vira lixo permanente.
window.salvarCarrinhoPdv = function (json) {
    sessionStorage.setItem('pdv_carrinho', json);
};

window.carregarCarrinhoPdv = function () {
    return sessionStorage.getItem('pdv_carrinho');
};

window.limparCarrinhoPdvStorage = function () {
    sessionStorage.removeItem('pdv_carrinho');
};
