// ── theme.js ─────────────────────────────────────────────────────────────
// Movido de dentro do index.html (09/09) — CSP bloqueava scripts inline
// sem hash/nonce, o que impedia o Portal de aplicar o tema salvo e de rolar
// o chat automaticamente. Como arquivo externo, entra em script-src 'self'
// sem precisar de exceção nenhuma na política.

// Dark mode
window.toggleDarkMode = function() {
    const html = document.documentElement;
    const current = html.getAttribute('data-theme');
    const next = current === 'dark' ? 'light' : 'dark';
    html.setAttribute('data-theme', next);
    localStorage.setItem('theme', next);
    return next;
};

// Chat scroll
window.scrollChatToBottom = function(id) {
    const el = document.getElementById(id);
    if (el) el.scrollTop = el.scrollHeight;
};

window.initTheme = function() {
    const saved = localStorage.getItem('theme') || 'light';
    document.documentElement.setAttribute('data-theme', saved);
    return saved;
};
