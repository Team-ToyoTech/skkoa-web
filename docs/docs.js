document.addEventListener("DOMContentLoaded", function () {
    const links = document.querySelectorAll(".top-menu a");
    const sections = document.querySelectorAll("main section");

    if (links.length && sections.length) {
        links[0].classList.add("active");
        sections[0].classList.add("active");
    }

    links.forEach((link) => {
        link.addEventListener("click", function (event) {
            event.preventDefault();
            const targetId = this.getAttribute("href").substring(1);

            links.forEach((l) => l.classList.remove("active"));
            sections.forEach((s) => s.classList.remove("active"));

            this.classList.add("active");
            const targetSection = document.getElementById(targetId);
            if (targetSection) {
                targetSection.classList.add("active");
            }
        });
    });
});
