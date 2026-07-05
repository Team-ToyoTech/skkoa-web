document.addEventListener("DOMContentLoaded", function () {
    try {
        const defaultCompilerDownloadPath =
            "/compiler/download/skkoa-windows.exe";
        const studioDownloadPath =
            "/download/studio/SKKOA-Studio-Setup-x64.exe";

        function startDownload(path, fileName) {
            const link = document.createElement("a");
            link.href = path;
            link.download = fileName;
            document.body.appendChild(link);
            link.click();
            setTimeout(() => {
                if (link.parentNode) {
                    link.parentNode.removeChild(link);
                }
            }, 0);
        }

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

        const downloadBtn = document.getElementById("download-compiler-btn");
        if (downloadBtn) {
            downloadBtn.addEventListener("click", function (e) {
                e.preventDefault();
                startDownload(defaultCompilerDownloadPath, "skkoa-windows.exe");
            });
        }

        const studioBtn = document.getElementById("download-studio-btn");
        if (studioBtn) {
            studioBtn.addEventListener("click", function (e) {
                e.preventDefault();
                startDownload(studioDownloadPath, "SKKOA-Studio-Setup-x64.exe");
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
                { passive: false },
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

const yearNow = document.getElementById("yearNow");
if (yearNow) {
    yearNow.textContent = new Date().getFullYear();
}
