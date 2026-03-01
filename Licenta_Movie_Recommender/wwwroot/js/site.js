document.addEventListener('DOMContentLoaded', () => {

    // --- BUTOANE ACTIUNE RAPIDA (WATCHLIST / VAZUT) ---
    document.body.addEventListener('submit', async (e) => {
        const form = e.target;

        // daca actiunea are loc pe un poster de film
        if (form.closest('.movie-card-overlay')) {
            e.preventDefault();
            e.stopPropagation();

            const btn = form.querySelector('button');
            if (!btn) return;
            const actionUrl = form.action;
            const overlay = form.closest('.movie-card-overlay');

            if (!form.dataset.originalAction) {
                if (actionUrl.includes('AddToWatchlist')) form.dataset.originalAction = 'watchlist';
                else if (actionUrl.includes('MarkAsWatched')) form.dataset.originalAction = 'watched';
                else if (actionUrl.includes('RemoveActivityStatus')) {
                    form.dataset.originalAction = btn.className.includes('btn-warning') ? 'watchlist' : 'watched';
                }
            }

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

                    const siblingForm = Array.from(overlay.querySelectorAll('form')).find(f => f.dataset.originalAction === 'watched');
                    if (siblingForm && siblingForm.action.includes('Remove')) {
                        const sBtn = siblingForm.querySelector('button');
                        sBtn.className = 'btn btn-outline-success btn-sm w-100 bg-dark shadow-sm';
                        sBtn.innerHTML = '<i class="bi bi-eye"></i> Văzut';
                        siblingForm.action = siblingForm.action.replace('RemoveActivityStatus', 'MarkAsWatched');
                    }
                }
                else if (actionUrl.includes('MarkAsWatched')) {
                    btn.className = 'btn btn-success btn-sm w-100 shadow-sm';
                    btn.innerHTML = '<i class="bi bi-check-circle-fill"></i> Vizionat';
                    form.action = form.action.replace('MarkAsWatched', 'RemoveActivityStatus');

                    const siblingForm = Array.from(overlay.querySelectorAll('form')).find(f => f.dataset.originalAction === 'watchlist');
                    if (siblingForm && siblingForm.action.includes('Remove')) {
                        const sBtn = siblingForm.querySelector('button');
                        sBtn.className = 'btn btn-outline-warning btn-sm w-100 shadow-sm';
                        sBtn.innerHTML = '<i class="bi bi-bookmark-plus"></i> Watchlist';
                        siblingForm.action = siblingForm.action.replace('RemoveActivityStatus', 'AddToWatchlist');
                    }
                }
                else if (actionUrl.includes('IgnoreMovie')) {
                    const card = form.closest('.col-lg-2, .col-md-3, .col-sm-4, .col-6');
                    card.style.transition = "all 0.3s ease";
                    card.style.opacity = "0";
                    card.style.transform = "scale(0.9)";
                    setTimeout(() => card.remove(), 300);
                }
                else if (actionUrl.includes('RemoveActivityStatus')) {
                    if (form.dataset.originalAction === 'watchlist') {
                        btn.className = 'btn btn-outline-warning btn-sm w-100 shadow-sm';
                        btn.innerHTML = '<i class="bi bi-bookmark-plus"></i> Watchlist';
                        form.action = form.action.replace('RemoveActivityStatus', 'AddToWatchlist');
                    } else if (form.dataset.originalAction === 'watched') {
                        btn.className = 'btn btn-outline-success btn-sm w-100 bg-dark shadow-sm';
                        btn.innerHTML = '<i class="bi bi-eye"></i> Văzut';
                        form.action = form.action.replace('RemoveActivityStatus', 'MarkAsWatched');
                    }
                }
            } catch (error) {
                btn.innerHTML = originalHtml;
                btn.disabled = false;
            }
        }
    });

    // --- REFRESH DINAMIC (AJAX) SECTIUNE DESCOPERA ---
    document.addEventListener('DOMContentLoaded', () => {
        const btnRefresh = document.getElementById('btnRefreshDiscover');
        const discoverGrid = document.getElementById('discoverGrid');

        if (btnRefresh && discoverGrid) {
            btnRefresh.addEventListener('click', async function () {
                
                if (this.disabled) return;

                
                const originalHtml = this.innerHTML;
                this.innerHTML = '<span class="spinner-border spinner-border-sm"></span>';
                this.disabled = true;

                try {
                   
                    const response = await fetch(`/Home/GetDiscoverMovies?t=${Date.now()}`, {
                        headers: { 'X-Requested-With': 'XMLHttpRequest' }
                    });

                    if (response.ok) {
                        const htmlSnippet = await response.text();

                        
                        if (htmlSnippet.trim() !== "") {
                            discoverGrid.innerHTML = htmlSnippet;
                        }
                    }
                } catch (error) {
                    console.error("Eroare la refresh:", error);
                } finally {
                    
                    this.innerHTML = originalHtml;
                    this.disabled = false;
                }
            });
        }
    });

    // --- BARA DE CAUTARE LIVE (PREVIEW) ---
    const searchInput = document.getElementById('searchInput');
    const searchDropdown = document.getElementById('searchPreviewDropdown');
    const searchResultsList = document.getElementById('searchResultsList');
    let debounceTimer;

    if (searchInput) {
        searchInput.addEventListener('input', function () {
            clearTimeout(debounceTimer);
            const query = this.value.trim();

            if (query.length < 2) {
                searchDropdown.style.display = 'none';
                return;
            }

            debounceTimer = setTimeout(async () => {
                try {
                    const response = await fetch(`/Movies/SearchPreview?q=${encodeURIComponent(query)}`);
                    const data = await response.json();

                    searchResultsList.innerHTML = '';

                    if (data.totalCount === 0) {
                        searchResultsList.innerHTML = `
                            <div class="list-group-item bg-transparent text-muted border-0 p-4 text-center">
                                <i class="bi bi-search display-6 d-block mb-2 opacity-25"></i>
                                Nu am găsit niciun film numit "<span class="text-light">${query}</span>".
                            </div>`;
                    } else {
                        data.movies.forEach(movie => {
                            searchResultsList.innerHTML += `
                                <a href="/Movies/Details/${movie.id}" class="list-group-item list-group-item-action d-flex align-items-center bg-transparent text-light border-bottom border-secondary" style="border-color: rgba(255,255,255,0.05) !important;">
                                    <img src="${movie.posterUrl}" class="rounded me-3 shadow-sm" style="width: 35px; height: 50px; object-fit: cover;" onerror="this.src='https://placehold.co/35x50/2b2b2b/ffffff?text=${encodeURIComponent(movie.title)}'">
                                    <div>
                                        <h6 class="mb-0 fw-bold fs-6">${movie.title}</h6>
                                    </div>
                                </a>
                            `;
                        });

                        if (data.totalCount > 5) {
                            const remaining = data.totalCount - 5;
                            searchResultsList.innerHTML += `
                                <a href="/Movies/Index?searchString=${encodeURIComponent(query)}" class="list-group-item list-group-item-action bg-dark text-center text-danger border-0 py-2 fw-bold" style="border-radius: 0 0 0.5rem 0.5rem; transition: background 0.2s;" onmouseover="this.className='list-group-item list-group-item-action bg-danger text-white text-center border-0 py-2 fw-bold'" onmouseout="this.className='list-group-item list-group-item-action bg-dark text-danger text-center border-0 py-2 fw-bold'">
                                    Vezi încă ${remaining} rezultate &raquo;
                                </a>`;
                        }
                    }
                    searchDropdown.style.display = 'block';
                } catch (e) {
                    console.error("Eroare cautare:", e);
                }
            }, 300);
        });

        document.addEventListener('click', function (e) {
            if (!searchInput.contains(e.target) && !searchDropdown.contains(e.target)) {
                searchDropdown.style.display = 'none';
            }
        });

        searchInput.addEventListener('focus', function () {
            if (this.value.trim().length >= 2 && searchResultsList.innerHTML !== '') {
                searchDropdown.style.display = 'block';
            }
        });
    }

    // --- LOGICA IGNORA PENTRU RECOMANDARI ---
    document.addEventListener('click', async function (e) {
        const btn = e.target.closest('.btn-ignore-rec');
        if (btn) {
            const movieId = btn.getAttribute('data-movie-id');
            const container = btn.closest('.movie-container');

            
            container.style.transition = 'all 0.4s ease';
            container.style.opacity = '0';
            container.style.transform = 'scale(0.8)';

            try {
                await fetch(`/Movies/Ignore/${movieId}`, { method: 'POST' });
                setTimeout(() => container.remove(), 400);
            } catch (err) {
                console.error("Eroare la ignorare:", err);
                container.style.opacity = '1';
                container.style.transform = 'scale(1)';
            }
        }
    });

});