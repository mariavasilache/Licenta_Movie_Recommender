//1. FUNCȚII DE RANDARE UI(SKELETONS & CARDS)

function getSkeletonsHtml(count = 12) {
    const skeleton = `
        <div class="col-lg-2 col-md-3 col-sm-4 col-6 mb-4">
            <div class="skeleton-poster rounded mb-2" style="aspect-ratio: 2/3;"></div>
            <div class="skeleton-text w-75 mx-auto"></div>
            <div class="skeleton-text w-50 mx-auto mt-1"></div>
        </div>`;
    return skeleton.repeat(count);
}

function createMovieCardHtml(movie, customOptions = {}) {
    const options = {
        showBadges: customOptions.showBadges || false,
        showDate: customOptions.showDate || false,
        isAdmin: customOptions.isAdmin || false,
        layout: customOptions.layout || 'grid'
    };

    const id = movie.id || movie.movieId;
    const title = movie.title || "N/A";
    const posterUrl = movie.posterUrl || "";
    const genres = movie.genres ? movie.genres.split('|')[0] : 'Fără gen';

    let badgeHtml = '';
    if (options.showBadges) {
        if (movie.rating > 0) {
            badgeHtml = `<span class="position-absolute badge bg-warning text-dark m-1 shadow-sm badge-glow" style="top: 5px; right: 15px; z-index: 5;"><i class="bi bi-star-fill"></i> ${movie.rating}</span>`;
        } else if (movie.status === 1) {
            badgeHtml = `<span class="position-absolute badge bg-warning text-dark m-1 shadow-sm badge-glow" style="top: 5px; right: 15px; z-index: 5;" title="In Watchlist"><i class="bi bi-bookmark-fill"></i></span>`;
        } else if (movie.status === 2) {
            badgeHtml = `<span class="position-absolute badge bg-success m-1 shadow-sm badge-glow" style="top: 5px; right: 15px; z-index: 5;" title="Vizionat"><i class="bi bi-eye-fill"></i></span>`;
        }
    }

    const dateHtml = (options.showDate && movie.dateAdded)
        ? `<small class="text-muted mt-auto" style="font-size: 0.75rem; opacity: 0.7;"><i class="bi bi-clock-history"></i> ${movie.dateAdded}</small>`
        : '';

    let buttonsHtml = '';
    if (options.isAdmin) {
        buttonsHtml = `<button type="button" class="btn btn-sm btn-danger btn-delete-movie shadow-sm" data-movie-id="${id}" data-movie-title="${title}" title="Șterge definitiv"><i class="bi bi-trash"></i></button>`;
    } else {
        const watchlistClass = movie.status === 1 ? 'btn-active-watchlist' : 'btn-outline-light';
        const watchlistIcon = movie.status === 1 ? 'bi-bookmark-fill' : 'bi-bookmark';
        const watchedClass = movie.status === 2 ? 'btn-active-watched' : 'btn-outline-light';
        const watchedIcon = movie.status === 2 ? 'bi-check-circle-fill' : 'bi-check-circle';

        buttonsHtml = `
            <button type="button" class="btn btn-sm ${watchlistClass} btn-watchlist-toggle shadow-sm" data-movie-id="${id}" title="Watchlist"><i class="bi ${watchlistIcon}"></i></button>
            <button type="button" class="btn btn-sm ${watchedClass} btn-watched-toggle shadow-sm" data-movie-id="${id}" title="Vizionat"><i class="bi ${watchedIcon}"></i></button>`;
    }

    if (options.layout === 'compact') {
        return `
        <div class="col-12 mb-2 movie-container">
            <div class="card bg-dark border-secondary d-flex flex-row align-items-center p-2 shadow-sm" style="border-color: rgba(255,255,255,0.05) !important;">
                <img src="${posterUrl}" class="rounded shadow-sm" style="width: 45px; height: 65px; object-fit: cover;" onerror="this.src='https://placehold.co/45x65/2b2b2b/ffffff?text=?'">
                <div class="ms-3 flex-grow-1 text-start overflow-hidden">
                    <a href="/Movies/Details/${id}" class="text-white text-decoration-none fw-bold text-truncate d-block">${title}</a>
                    <div class="text-muted small text-truncate">${genres}</div>
                </div>
                <div class="d-flex gap-2 pe-2">${buttonsHtml}</div>
            </div>
        </div>`;
    }

    return `
    <div class="col-lg-2 col-md-3 col-sm-4 col-6 mb-4 movie-container">
        <div class="card bg-transparent border-0 movie-card position-relative h-100 shadow-sm">
            <div class="movie-actions position-absolute top-0 end-0 p-2 d-flex flex-column gap-2" style="z-index: 10;">${buttonsHtml}</div>
            <a href="/Movies/Details/${id}" class="text-decoration-none d-flex align-items-center justify-content-center position-relative bg-dark rounded shadow skeleton-poster text-center p-3" style="aspect-ratio: 2/3; overflow: hidden;">
                <span class="text-secondary fw-bold" style="z-index: 1; font-size: 0.85rem;">${title}</span>
                <img src="${posterUrl}" class="position-absolute top-0 start-0 w-100 h-100" style="object-fit: cover; opacity: 0; transition: opacity 0.4s ease-in-out; z-index: 2;"
                     onload="this.style.opacity='1'; this.parentElement.classList.remove('skeleton-poster');"
                     onerror="this.style.display='none'; this.parentElement.classList.remove('skeleton-poster');">
            </a>
            ${badgeHtml}
            <div class="card-body px-1 py-2 text-center d-flex flex-column" style="min-height: 65px;">
                <h6 class="card-title text-light text-truncate mb-1" style="font-size: 0.92rem;">${title}</h6>
                ${dateHtml}
            </div>
        </div>
    </div>`;
}


//2. INITIALIZARE SI EVENIMENTE GLOBALE

document.addEventListener('DOMContentLoaded', () => {

    //REFRESH DISCOVER (HOME PAGE)
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
                const response = await fetch(`/Home/GetDiscoverMovies?t=${Date.now()}`, { headers: { 'X-Requested-With': 'XMLHttpRequest' } });
                if (response.ok) {
                    const htmlSnippet = await response.text();
                    if (htmlSnippet.trim() !== "") discoverGrid.innerHTML = htmlSnippet;
                }
            } catch (error) { console.error(error); }
            finally {
                this.innerHTML = originalHtml;
                this.disabled = false;
            }
        });
    }


    // CLICK UNIVERSAL (WATCHLIST, WATCHED, IGNORE, DELETE)
    document.addEventListener('click', async function (e) {
        const btnWatchlist = e.target.closest('.btn-watchlist-toggle');
        const btnWatched = e.target.closest('.btn-watched-toggle');
        const btnIgnore = e.target.closest('.btn-ignore-rec');
        const btnDelete = e.target.closest('.btn-delete-movie');

        // 1. Logica Watchlist / Vizionat 
        if (btnWatchlist || btnWatched) {
            e.preventDefault();
            const btn = btnWatchlist || btnWatched;
            const container = btn.closest('.movie-container');
            const movieId = btn.getAttribute('data-movie-id');
            const isWatchlistAction = !!btnWatchlist;
            const url = isWatchlistAction ? '/Movies/ToggleWatchlist' : '/Movies/ToggleWatched';

            btn.disabled = true;
            const fd = new FormData();
            fd.append('movieId', movieId);

            try {
                const response = await fetch(url, { method: 'POST', body: fd });
                if (response.ok) {
                    const result = await response.json();
                    const icon = btn.querySelector('i');

                    //celalat buton de pe card
                    const otherBtnW = container.querySelector('.btn-watchlist-toggle');
                    const otherBtnV = container.querySelector('.btn-watched-toggle');

                    // Schimb clase vizual fara reincarcare pag
                    if (isWatchlistAction) {
                        if (result.inWatchlist) {
                            btn.classList.remove('btn-outline-light');
                            btn.classList.add('btn-active-watchlist');
                            icon.className = 'bi bi-bookmark-fill';

                            if (otherBtnV) {
                                otherBtnV.classList.remove('btn-active-watched');
                                otherBtnV.classList.add('btn-outline-light');
                                otherBtnV.querySelector('i').className = 'bi bi-check-circle';
                            }

                        } else {
                            btn.classList.remove('btn-active-watchlist');
                            btn.classList.add('btn-outline-light');
                            icon.className = 'bi bi-bookmark';
                        }
                    } else {
                        if (result.isWatched) {
                            btn.classList.remove('btn-outline-light');
                            btn.classList.add('btn-active-watched');
                            icon.className = 'bi bi-check-circle-fill';

                            if (otherBtnW) {
                                otherBtnW.classList.remove('btn-active-watchlist');
                                otherBtnW.classList.add('btn-outline-light');
                                otherBtnW.querySelector('i').className = 'bi bi-bookmark';
                            }
                        } else {
                            btn.classList.remove('btn-active-watched');
                            btn.classList.add('btn-outline-light');
                            icon.className = 'bi bi-check-circle';
                        }
                    }
                
                } else {
                     const err = await response.json();
                     alert("Eroare: " + err.error);
                 }
        } catch (err) { console.error(err); }
        finally { btn.disabled = false; }
    }
        

        // 2. Logica Ignora
        if (btnIgnore) {
            e.preventDefault();
            const movieId = btnIgnore.getAttribute('data-movie-id');
            const container = btnIgnore.closest('.movie-container');
            if (container) {
                container.style.transition = 'all 0.4s ease';
                container.style.opacity = '0';
                container.style.transform = 'scale(0.8)';
            }

            const fd = new FormData();
            fd.append('movieId', movieId);

            try {
                const response = await fetch('/Movies/Ignore', { method: 'POST', body: fd });
                if (response.ok && container) setTimeout(() => container.remove(), 400);
            } catch (err) { console.error(err); }
        }

        // 3. Logica Admin Delete
        if (btnDelete) {
            e.preventDefault();
            const movieId = btnDelete.getAttribute('data-movie-id');
            const container = btnDelete.closest('.movie-container') || btnDelete.closest('tr');

            if (confirm("Stergi definitiv filmul?")) {
                const fd = new FormData();
                fd.append('id', movieId);
                try {
                    const response = await fetch(`/Admin/DeleteMovie`, { method: 'POST', body: fd });
                    if (response.ok && container) container.remove();
                } catch (err) { console.error(err); }
            }
        }
    });
    

    //SEARCH PREVIEW
    const searchInput = document.getElementById('searchInput');
    const searchDropdown = document.getElementById('searchPreviewDropdown');
    const searchResultsList = document.getElementById('searchResultsList');
    let debounceTimer;

    if (searchInput) {
        searchInput.addEventListener('input', function () {
            clearTimeout(debounceTimer);
            const query = this.value.trim();
            if (query.length < 2) { searchDropdown.style.display = 'none'; return; }

            debounceTimer = setTimeout(async () => {
                try {
                    const response = await fetch(`/Movies/SearchPreview?q=${encodeURIComponent(query)}`);
                    const data = await response.json();
                    searchResultsList.innerHTML = '';

                    if (data.totalCount === 0) {
                        searchResultsList.innerHTML = `<div class="p-3 text-muted">Niciun rezultat.</div>`;
                    } else {
                        data.movies.forEach(movie => {
                            searchResultsList.innerHTML += `
                                <a href="/Movies/Details/${movie.id}" class="list-group-item list-group-item-action bg-dark text-white border-secondary">
                                    <img src="${movie.posterUrl}" width="30" class="me-2 rounded"> ${movie.title}
                                </a>`;
                        });
                    }
                    searchDropdown.style.display = 'block';
                } catch (e) { console.error(e); }
            }, 300);
        });
    }
    
});
