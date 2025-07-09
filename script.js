document.addEventListener("DOMContentLoaded", function () {
    try {
        const buttons = document.querySelectorAll("button");
        buttons.forEach((btn) => {
            btn.addEventListener("mousedown", (e) => {
                (e.currentTarget || btn).style.transform = "scale(0.96)";
            });
            btn.addEventListener("mouseup", (e) => {
                (e.currentTarget || btn).style.transform = "scale(1)";
            });
            btn.addEventListener("mouseleave", (e) => {
                (e.currentTarget || btn).style.transform = "scale(1)";
            });
        });

        const onlineBtn = document.getElementById("online-compiler-btn");
        if (onlineBtn) {
            onlineBtn.addEventListener("click", function (e) {
                e.preventDefault();
                window.open("/compiler", "_blank");
            });
        }

        const cube = document.querySelector(".cube3d-inner");
        if (cube) {
            cube.style.animation = "none";
            window.addEventListener("mousemove", function (e) {
                const xRatio = e.clientX / window.innerWidth;
                const yRatio = e.clientY / window.innerHeight;
                const rotY = (xRatio - 0.5) * 180;
                const rotX = (0.5 - yRatio) * 90;
                cube.style.transform = `rotateX(${
                    rotX - 20
                }deg) rotateY(${rotY}deg)`;
            });

            window.addEventListener(
                "touchmove",
                function (e) {
                    if (!e.touches || !e.touches[0]) return;
                    const xRatio = e.touches[0].clientX / window.innerWidth;
                    const yRatio = e.touches[0].clientY / window.innerHeight;
                    const rotY = (xRatio - 0.5) * 180;
                    const rotX = (0.5 - yRatio) * 90;
                    cube.style.transform = `rotateX(${
                        rotX - 20
                    }deg) rotateY(${rotY}deg)`;
                },
                { passive: false }
            );
        }

        const cubeBtn = document.getElementById("cube-toggle-btn");
        const cubeMenu = document.getElementById("cube-menu");
        if (cubeBtn && cubeMenu) {
            cubeBtn.addEventListener("click", function (e) {
                e.stopPropagation();
                if (
                    cubeMenu.style.display === "none" ||
                    cubeMenu.style.display === ""
                ) {
                    cubeMenu.style.display = "flex";
                } else {
                    cubeMenu.style.display = "none";
                }
            });

            window.addEventListener("click", function (e) {
                if (cubeMenu.style.display === "flex") {
                    cubeMenu.style.display = "none";
                }
            });

            cubeMenu.addEventListener("click", function (e) {
                e.stopPropagation();
            });
        }
    } catch (err) {
        console.error("Button event error:", err);
    }
});
