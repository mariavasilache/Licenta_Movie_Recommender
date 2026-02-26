document.addEventListener('DOMContentLoaded', () => {
    
    document.body.addEventListener('submit', async (e) => {
        const form = e.target;

        // daca actiunea are loc pe un poster de film 
        if (form.closest('.movie-card-overlay')) {
            e.preventDefault(); 

            const btn = form.querySelector('button');
            const actionUrl = form.action;
            const originalHtml = btn.innerHTML;

            btn.innerHTML = '<span class="spinner-border spinner-border-sm"></span>';
            btn.disabled = true;

            try {
               
                await fetch(actionUrl, { method: 'POST', body: new FormData(form) });

                btn.disabled = false;

              
                if (actionUrl.includes('AddToWatchlist')) {
                    btn.className = 'btn btn-warning btn-sm w-100 fw-bold shadow-sm';
                    btn.innerHTML = '<i class="bi bi-bookmark-check-fill"></i> În Watchlist';
                    form.action = form.action.replace('AddToWatchlist', 'RemoveActivityStatus');
                }
                else if (actionUrl.includes('MarkAsWatched')) {
                    btn.className = 'btn btn-success btn-sm w-100 shadow-sm';
                    btn.innerHTML = '<i class="bi bi-check-circle-fill"></i> Vizionat';
                    form.action = form.action.replace('MarkAsWatched', 'RemoveActivityStatus');
                }
                else if (actionUrl.includes('IgnoreMovie')) {
                    const card = form.closest('.col-lg-2, .col-md-3, .col-sm-4, .col-6');
                    card.style.transition = "all 0.3s ease";
                    card.style.opacity = "0";
                    card.style.transform = "scale(0.9)";
                    setTimeout(() => card.remove(), 300);
                }
                else if (actionUrl.includes('RemoveActivityStatus')) {
                    btn.className = 'btn btn-outline-light btn-sm w-100';
                    btn.innerHTML = '<i class="bi bi-dash-circle"></i> Anulat';
                }
            } catch (error) {
                btn.innerHTML = originalHtml;
                btn.disabled = false;
            }
        }
    });
});