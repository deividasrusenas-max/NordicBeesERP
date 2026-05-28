window.cashflowChart = null;
window.renderCashFlowChart = function(labels, amounts, colors) {
    const ctx = document.getElementById('cashFlowChart');
    if (!ctx) return;
    if (window.cashflowChart) window.cashflowChart.destroy();
    window.cashflowChart = new Chart(ctx, {
        type: 'bar',
        data: { labels, datasets: [{ data: amounts, backgroundColor: colors, borderRadius: 6, borderSkipped: false, barPercentage: 0.45, categoryPercentage: 0.7 }] },
        options: {
            responsive: true,
            plugins: { legend: { display: false }, tooltip: { callbacks: { label: c => c.raw.toLocaleString('lt-LT') + ' €' }}},
            scales: {
                y: { beginAtZero: true, grid: { color: '#f3f4f6' }, ticks: { callback: v => v.toLocaleString('lt-LT') + ' €' }},
                x: { grid: { display: false }}
            }
        }
    });
};
