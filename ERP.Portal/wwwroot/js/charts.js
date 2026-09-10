// ── charts.js ────────────────────────────────────────────────────────────
// Movido de dentro do index.html (09/09) — mesmo motivo do theme.js. Além
// disso, o Chart.js em si passou a ser servido localmente (wwwroot/lib/),
// não mais via CDN (cdn.jsdelivr.net) — a CSP não precisa mais liberar
// nenhum domínio externo pra isso.

let _charts = {};

window.criarGraficoBarras = function(id, labels, dados) {
    if (_charts[id]) { _charts[id].destroy(); }
    const ctx = document.getElementById(id);
    if (!ctx) return;
    _charts[id] = new Chart(ctx, {
        type: 'bar',
        data: {
            labels: labels,
            datasets: [{
                label: 'Faturamento',
                data: dados,
                backgroundColor: 'rgba(30, 98, 166, 0.8)',
                borderColor: '#1E62A6',
                borderWidth: 2,
                borderRadius: 6
            }]
        },
        options: {
            responsive: true,
            plugins: { legend: { display: false } },
            scales: {
                y: {
                    beginAtZero: true,
                    ticks: {
                        callback: v => 'R$ ' + v.toLocaleString('pt-BR', {minimumFractionDigits:0})
                    }
                }
            }
        }
    });
};

window.criarGraficoPizza = function(id, labels, dados, cores) {
    if (_charts[id]) { _charts[id].destroy(); }
    const ctx = document.getElementById(id);
    if (!ctx) return;
    _charts[id] = new Chart(ctx, {
        type: 'doughnut',
        data: {
            labels: labels,
            datasets: [{
                data: dados,
                backgroundColor: cores,
                borderWidth: 2,
                borderColor: '#fff'
            }]
        },
        options: {
            responsive: true,
            cutout: '65%',
            plugins: {
                legend: {
                    position: 'bottom',
                    labels: { font: { size: 11 }, padding: 10 }
                }
            }
        }
    });
};

window.criarGraficoLinha = function(id, labels, datasets) {
    if (_charts[id]) { _charts[id].destroy(); }
    const ctx = document.getElementById(id);
    if (!ctx) return;
    _charts[id] = new Chart(ctx, {
        type: 'line',
        data: { labels, datasets },
        options: {
            responsive: true,
            tension: 0.4,
            plugins: { legend: { position: 'bottom' } },
            scales: { y: { beginAtZero: true } }
        }
    });
};
