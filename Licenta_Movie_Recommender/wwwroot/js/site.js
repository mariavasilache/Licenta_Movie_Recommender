function getSkeletonsHtml(count = 12) {
    const skeleton = `
        <div class="col-lg-2 col-md-3 col-sm-4 col-6 mb-4">
            <div class="skeleton-poster rounded mb-2" style="aspect-ratio: 2/3;"></div>
            <div class="skeleton-text w-75 mx-auto"></div>
            <div class="skeleton-text w-50 mx-auto mt-1"></div>
        </div>`;
    return skeleton.repeat(count);
}

function createMovieCardHtml(movie, options = { showBadges: false, showDate: false }) {
    const id = movie.id || movie.movieId;

    let badgeHtml = '';
    if (options.showBadges) {
        if (movie.rating > 0) {
            badgeHtml = `<span class="position-absolute badge bg-warning text-dark m-1 shadow-sm" style="top: 5px; right: 15px; z-index: 5;"><i class="bi bi-star-fill"></i> ${movie.rating}</span>`;
        } else if (movie.status === 1) {
            badgeHtml = `<span class="position-absolute badge bg-warning text-dark m-1 shadow-sm" style="top: 5px; right: 15px; z-index: 5;" title="În Watchlist"><i class="bi bi-bookmark-fill"></i></span>`;
        } else if (movie.status === 2) {
            badgeHtml = `<span class="position-absolute badge bg-success m-1 shadow-sm" style="top: 5px; right: 15px; z-index: 5;" title="Vizionat"><i class="bi bi-eye-fill"></i></span>`;
        }
    }

    const dateHtml = (options.showDate && movie.dateAdded)
        ? `<small class="text-muted mt-auto" style="font-size: 0.75rem; opacity: 0.7;"><i class="bi bi-clock-history"></i> ${movie.dateAdded}</small>`
        : '';

    return `
    <div class="col-lg-2 col-md-3 col-sm-4 col-6 mb-4 movie-container">
        <div class="card bg-transparent border-0 movie-card position-relative h-100 shadow-sm">
            <a href="/Movies/Details/${id}" class="text-decoration-none d-flex align-items-center justify-content-center position-relative bg-dark rounded shadow skeleton-poster text-center p-3" style="aspect-ratio: 2/3; overflow: hidden;">
                <span class="text-secondary fw-bold" style="z-index: 1; font-size: 0.85rem;">${movie.title}</span>
                <img src="${movie.posterUrl}" class="position-absolute top-0 start-0 w-100 h-100" style="object-fit: cover; opacity: 0; transition: opacity 0.4s ease-in-out; z-index: 2;"
                     onload="this.style.opacity='1'; this.parentElement.classList.remove('skeleton-poster');"
                     onerror="this.style.display='none'; this.parentElement.classList.remove('skeleton-poster');">
            </a>
            ${badgeHtml}
            <div class="card-body px-1 py-2 text-center d-flex flex-column" style="min-height: 65px;">
                <h6 class="card-title text-light text-truncate mb-1" style="font-size: 0.92rem;">${movie.title}</h6>
                ${dateHtml}
            </div>
        </div>
    </div>`;
}


document.addEventListener('DOMContentLoaded', () => {
    
    // --- 1. REFRESH DINAMIC (SECTIUNE DISCOVER) ---
    const btnRefresh = document.getElementById('btnRefreshDiscover');
    const discoverGrid = document.getElementById('discoverGrid');

    if (btnRefresh && discoverGrid) {
        btnRefresh.addEventListener('click', async function () {
            if (this.disabled) return;

            const originalHtml = this.innerHTML;
            this.innerHTML = '<span class="spinner-border spinner-border-sm"></span>';
            this.disabled = true;

            discoverGrid.innerHTML = getSkeletonsHtml(12);

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

    // --- 2. DELEGARE EVENIMENTE (WATCHLIST, VAZUT, IGNORE) ---
    
    document.addEventListener('click', async function (e) {

        // TOGGLE WATCHLIST (Bookmark)
        const btnWatchlist = e.target.closest('.btn-watchlist-toggle');
        if (btnWatchlist) {
            const movieId = btnWatchlist.getAttribute('data-movie-id');
            const icon = btnWatchlist.querySelector('i');
            btnWatchlist.disabled = true;

            try {
                const response = await fetch(`/Movies/ToggleWatchlist/${movieId}`, { method: 'POST' });
                if (response.ok) {
                    const result = await response.json();
                    // Schimbam DOAR clasele si iconita (fara text, ramane patrat)
                    if (result.inWatchlist) {
                        btnWatchlist.classList.replace('btn-outline-light', 'btn-warning');
                        icon.classList.replace('bi-bookmark', 'bi-bookmark-fill');
                    } else {
                        btnWatchlist.classList.replace('btn-warning', 'btn-outline-light');
                        icon.classList.replace('bi-bookmark-fill', 'bi-bookmark');
                    }
                }
            } catch (err) { console.error(err); }
            finally { btnWatchlist.disabled = false; }
        }

        // TOGGLE VAZUT (Checkmark)
        const btnWatched = e.target.closest('.btn-watched-toggle');
        if (btnWatched) {
            const movieId = btnWatched.getAttribute('data-movie-id');
            const icon = btnWatched.querySelector('i');
            btnWatched.disabled = true;

            try {
                const response = await fetch(`/Movies/ToggleWatched/${movieId}`, { method: 'POST' });
                if (response.ok) {
                    const result = await response.json();
                    
                    if (result.isWatched) {
                        btnWatched.classList.replace('btn-outline-light', 'btn-success');
                        icon.classList.replace('bi-check-circle', 'bi-check-circle-fill');
                    } else {
                        btnWatched.classList.replace('btn-success', 'btn-outline-light');
                        icon.classList.replace('bi-check-circle-fill', 'bi-check-circle');
                    }
                }
            } catch (err) { console.error(err); }
            finally { btnWatched.disabled = false; }
        }

        // C. LOGICA IGNORA (PENTRU RECOMANDARI)
        const btnIgnore = e.target.closest('.btn-ignore-rec');
        if (btnIgnore) {
            const movieId = btnIgnore.getAttribute('data-movie-id');
            const container = btnIgnore.closest('.movie-container');

            container.style.transition = 'all 0.4s ease';
            container.style.opacity = '0';
            container.style.transform = 'scale(0.8)';

            try {
                const response = await fetch(`/Movies/Ignore/${movieId}`, { method: 'POST' });
                if (response.ok) {
                    setTimeout(() => container.remove(), 400);
                } else {
                    container.style.opacity = '1';
                    container.style.transform = 'scale(1)';
                }
            } catch (err) {
                console.error("Eroare la ignorare:", err);
                container.style.opacity = '1';
                container.style.transform = 'scale(1)';
            }
        }
    });

    // --- 3. BARA DE CAUTARE LIVE (PREVIEW) ---
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
                                    <img src="${movie.posterUrl}" class="rounded me-3 shadow-sm" style="width: 35px; height: 50px; object-fit: cover;" onerror="this.src='https://placehold.co/35x50/2b2b2b/ffffff?text=?'">
                                    <div>
                                        <h6 class="mb-0 fw-bold fs-6">${movie.title}</h6>
                                    </div>
                                </a>
                            `;
                        });

                        if (data.totalCount > 5) {
                            const remaining = data.totalCount - 5;
                            searchResultsList.innerHTML += `
                                <a href="/Movies/Index?searchString=${encodeURIComponent(query)}" class="list-group-item list-group-item-action bg-dark text-center text-danger border-0 py-2 fw-bold">
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

        // inchidere dropdown la click in afara
        document.addEventListener('click', function (e) {
            if (!searchInput.contains(e.target) && !searchDropdown.contains(e.target)) {
                searchDropdown.style.display = 'none';
            }
        });
    }
});