// NoufirTours - Global JS

// -------------------------------
// Password toggle (delegated)
// -------------------------------
document.addEventListener("click", function (e) {
    const btn = e.target.closest("[data-nt-toggle='password']");
    if (!btn) return;

    e.preventDefault();
    e.stopPropagation();

    const inputId = btn.getAttribute("data-target");
    if (!inputId) return;

    const pass = document.getElementById(inputId);
    if (!pass) return;

    const eyeOpen = btn.querySelector("[data-eye-open]");
    const eyeClosed = btn.querySelector("[data-eye-closed]");

    const hidden = pass.type === "password";
    pass.type = hidden ? "text" : "password";

    if (eyeOpen) eyeOpen.style.display = hidden ? "none" : "";
    if (eyeClosed) eyeClosed.style.display = hidden ? "" : "none";

    btn.setAttribute("aria-label", hidden ? "Hide password" : "Show password");
    btn.setAttribute("title", hidden ? "Hide password" : "Show password");

    pass.focus({ preventScroll: true });
});

// -------------------------------
// Loader + fake progress (ONLY here)
// -------------------------------
(function () {
    const loader = document.getElementById("ntLoader");
    const bar = document.getElementById("ntProgressBar");
    const pct = document.getElementById("ntProgressPct");
    if (!loader || !bar || !pct) return;

    let showTimer = null;
    let progTimer = null;
    let val = 0;

    function setProgress(x) {
        val = Math.max(0, Math.min(100, x));
        bar.style.width = val + "%";
        pct.textContent = val + "%";
    }

    function startProgress() {
        clearInterval(progTimer);
        val = 0;
        setProgress(0);
        progTimer = setInterval(() => {
            if (val < 90) setProgress(val + Math.max(1, Math.round((90 - val) * 0.06)));
        }, 80);
    }

    function finishProgress() {
        clearInterval(progTimer);
        progTimer = null;
        setProgress(100);
    }

    function showLoader() {
        clearTimeout(showTimer);
        showTimer = setTimeout(() => {
            loader.style.display = "flex";
            loader.style.pointerEvents = "auto";
            loader.setAttribute("aria-hidden", "false");
            startProgress();
        }, 120);
    }

    function hardHideLoader() {
        clearTimeout(showTimer);
        showTimer = null;
        clearInterval(progTimer);
        progTimer = null;

        // hide
        loader.style.pointerEvents = "none";
        loader.style.display = "none";
        loader.setAttribute("aria-hidden", "true");
        setProgress(0);

        // ✅ SAFETY unlock
        document.documentElement.style.pointerEvents = "";
        document.body.style.pointerEvents = "";

        document.body.classList.remove("modal-open");
        document.body.style.overflow = "";
        document.body.style.paddingRight = "";

        // kill any leftover bootstrap backdrops
        document.querySelectorAll(".modal-backdrop").forEach(b => b.remove());
    }

    function hideLoader() {
        finishProgress();
        setTimeout(hardHideLoader, 60);
    }

    // expose globally
    window.NTLoader = { show: showLoader, hide: hideLoader, hardHide: hardHideLoader };

    // always hide after load/back-forward cache
    window.addEventListener("load", hideLoader);
    window.addEventListener("pageshow", hideLoader);

    // ✅ CRITICAL: any modal open/close => hide loader immediately
    document.addEventListener("show.bs.modal", hardHideLoader);
    document.addEventListener("shown.bs.modal", hardHideLoader);
    document.addEventListener("hidden.bs.modal", hardHideLoader);

    // ignore UI toggles (modal/dropdown/offcanvas)
    function isUiToggleClick(target) {
        return !!target.closest(
            '[data-bs-toggle="modal"],[data-bs-toggle="dropdown"],[data-bs-toggle="offcanvas"],.modal,.dropdown-menu,.offcanvas'
        );
    }

    // show loader on real navigation (anchors only)
    document.addEventListener("click", function (e) {
        if (isUiToggleClick(e.target)) {
            hardHideLoader();
            return;
        }

        const a = e.target.closest("a");
        if (!a) return;

        const href = a.getAttribute("href");
        if (!href || href.startsWith("#") || href.startsWith("javascript:")) return;

        if (a.hasAttribute("data-nt-no-loader")) return;

        if (a.target === "_blank" || e.ctrlKey || e.metaKey || e.shiftKey || e.altKey) return;

        try {
            const url = new URL(href, window.location.href);
            if (url.origin !== window.location.origin) return;
        } catch {
            return;
        }

        showLoader();
    });

    // show loader on submits (except modal)
    document.addEventListener("submit", function (e) {
        if (e.target && e.target.closest && e.target.closest(".modal")) {
            hardHideLoader();
            return;
        }

        const submitter = e.submitter;
        if (submitter && submitter.hasAttribute("data-nt-no-loader")) return;

        showLoader();
    });

    // ESC safety
    document.addEventListener("keydown", function (e) {
        if (e.key === "Escape") hardHideLoader();
    });
})();