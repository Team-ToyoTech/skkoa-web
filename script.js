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
            onlineBtn.addEventListener("click", function () {
                const newWin = window.open("/compiler", "_blank");
                if (newWin) {
                    try {
                        newWin.opener = null;
                    } catch (e) {}
                }
            });
        }

        const downloadBtn = document.getElementById("download-btn");
        if (downloadBtn) {
            downloadBtn.addEventListener("click", function () {
                alert("다운로드 기능은 현재 지원되지 않습니다.");
            });
        }
    } catch (err) {
        console.error("Button event error:", err);
    }
});
