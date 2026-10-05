(function () {
    // ---- live clock in the title bar ----
    const clock = document.getElementById("clock");
    function tick() {
        if (!clock) return;
        const d = new Date();
        const p = (n) => String(n).padStart(2, "0");
        clock.textContent = `${p(d.getHours())}:${p(d.getMinutes())}:${p(d.getSeconds())}`;
    }
    tick();
    setInterval(tick, 1000);

    // ---- command bar ----
    const cmd = document.getElementById("cmd");

    function run(raw) {
        const input = raw.trim();
        if (!input) return;
        const parts = input.split(/\s+/);
        const verb = parts[0].toLowerCase();
        const arg = parts.slice(1).join(" ");

        const sections = {
            dashboard: "/Home/Index", home: "/Home/Index", ls: "/Home/Index",
            fixtures: "/Home/Fixtures", games: "/Home/Fixtures", schedule: "/Home/Fixtures",
            bracket: "/Home/Bracket", knockout: "/Home/Bracket",
            teams: "/Home/Teams", clear: "/Home/Index"
        };

        if (sections[verb]) { window.location.href = sections[verb]; return; }

        if ((verb === "open" || verb === "team" || verb === "cat") && arg) {
            const code = resolveTeam(arg);
            if (code) { window.location.href = "/Home/Team/" + code; return; }
            flash("team not found: " + arg);
            return;
        }
        if (verb === "match" && arg) { window.location.href = "/Home/Match/" + arg.toUpperCase(); return; }
        if (verb === "help" || verb === "man") {
            flash("commands: dashboard · fixtures · bracket · teams · open <TEAM> · match <ID>");
            return;
        }
        // bare team name / code
        const code = resolveTeam(input);
        if (code) { window.location.href = "/Home/Team/" + code; return; }
        flash("unknown command: " + input + "  (try: help)");
    }

    function resolveTeam(q) {
        q = q.toLowerCase();
        const list = window.__TEAMS__ || [];
        let hit = list.find((t) => t.code.toLowerCase() === q);
        if (hit) return hit.code;
        hit = list.find((t) => t.name.toLowerCase() === q);
        if (hit) return hit.code;
        hit = list.find((t) => t.name.toLowerCase().startsWith(q));
        if (hit) return hit.code;
        hit = list.find((t) => t.name.toLowerCase().includes(q));
        return hit ? hit.code : null;
    }

    function flash(msg) {
        if (!cmd) return;
        const old = cmd.value;
        cmd.value = "";
        cmd.placeholder = msg;
        cmd.classList.add("err");
        setTimeout(() => { cmd.classList.remove("err"); }, 1600);
    }

    if (cmd) {
        cmd.addEventListener("keydown", (e) => {
            if (e.key === "Enter") { run(cmd.value); }
            if (e.key === "Escape") { cmd.value = ""; cmd.blur(); }
        });
    }

    // ---- "/" focuses the command bar ----
    document.addEventListener("keydown", (e) => {
        if (e.key === "/" && document.activeElement !== cmd) {
            e.preventDefault();
            cmd && cmd.focus();
        }
    });

    // ---- system-alert popup ----
    const modal = document.getElementById("alertModal");
    function openModal() {
        if (!modal) return;
        modal.hidden = false;
        document.body.style.overflow = "hidden";
    }
    function closeModal() {
        if (!modal) return;
        modal.hidden = true;
        document.body.style.overflow = "";
    }
    if (modal) {
        // Auto-open whenever you land on the dashboard.
        if (modal.dataset.autoshow === "1") openModal();

        modal.addEventListener("click", (e) => {
            if (e.target === modal || e.target.closest("[data-close]")) closeModal();
        });
        document.addEventListener("keydown", (e) => {
            if (e.key === "Escape" && !modal.hidden) closeModal();
        });
    }
    // The title-bar warning chips re-open the popup from any page.
    document.querySelectorAll(".warn-chip").forEach((chip) => {
        chip.addEventListener("click", openModal);
    });
})();
