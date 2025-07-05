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

        // Online Compiler button
        const onlineBtn = document.getElementById("online-compiler-btn");
        if (onlineBtn) {
            onlineBtn.addEventListener("click", function (e) {
                e.preventDefault();
                window.open("/compiler", "_blank");
            });
        }
        // Download Compiler button
        const downloadBtn = document.getElementById("download-btn");
        if (downloadBtn) {
            downloadBtn.addEventListener("click", function (e) {
                e.preventDefault();
                alert("다운로드 기능은 현재 지원되지 않습니다.");
            });
        }

        // 오른쪽 아래 3D 큐브 마우스 위치에 따라 회전
        const cube = document.querySelector(".cube3d-inner");
        if (cube) {
            // 자동 회전 애니메이션 제거
            cube.style.animation = "none";
            // 마우스 위치에 따라 회전
            window.addEventListener("mousemove", function (e) {
                // 윈도우 전체 기준으로 비율 계산
                const xRatio = e.clientX / window.innerWidth;
                const yRatio = e.clientY / window.innerHeight;
                // -90~90deg, -45~45deg 범위로 회전
                const rotY = (xRatio - 0.5) * 180;
                const rotX = (0.5 - yRatio) * 90;
                cube.style.transform = `rotateX(${
                    rotX - 20
                }deg) rotateY(${rotY}deg)`;
            });
            // 터치도 지원 (터치 위치 기준)
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

        // 큐브 클릭 시 메뉴 토글
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
            // 메뉴 바깥 클릭 시 닫기
            window.addEventListener("click", function (e) {
                if (cubeMenu.style.display === "flex") {
                    cubeMenu.style.display = "none";
                }
            });
            // 메뉴 클릭 시 닫힘 방지
            cubeMenu.addEventListener("click", function (e) {
                e.stopPropagation();
            });
            // Online/Download 버튼 기능 연결
            const onlineMenu = document.getElementById("cube-menu-online");
            if (onlineMenu) {
                onlineMenu.addEventListener("click", function () {
                    window.open("/compiler", "_blank");
                });
            }
            const downloadMenu = document.getElementById("cube-menu-download");
            if (downloadMenu) {
                downloadMenu.addEventListener("click", function () {
                    alert("다운로드 기능은 현재 지원되지 않습니다.");
                });
            }
        }
    } catch (err) {
        console.error("Button event error:", err);
    }
});
