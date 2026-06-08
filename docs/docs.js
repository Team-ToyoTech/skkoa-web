const docs = [
    ["index", "개요"],
    ["introduction", "소개"],
    ["getting-started", "시작하기"],
    ["syntax", "문법"],
    ["types", "자료형"],
    ["variables", "변수와 상수"],
    ["control-flow", "제어 흐름"],
    ["functions", "함수"],
    ["arrays", "배열"],
    ["strings", "문자열"],
    ["pointers", "포인터"],
    ["data-structures", "자료구조"],
    ["standard-library", "표준 라이브러리"],
    ["compiler", "컴파일러"],
    ["examples", "예제"],
    ["grammar", "문법 요약"],
];

const skkoaKeywords = [
    "아니면만약",
    "시작",
    "끝",
    "변수",
    "상수",
    "출력",
    "입력",
    "만약",
    "이면",
    "아니면",
    "동안",
    "반복",
    "함수",
    "반환",
    "구조체",
    "가져오기",
    "정수",
    "실수",
    "논리",
    "문자",
    "문자열",
    "없음",
    "참",
    "거짓",
    "그리고",
    "또는",
    "아님",
    "주소",
    "값",
    "포인터",
    "부터",
    "까지",
    "할당",
    "해제",
    "길이",
    "비교",
    "부분문자열",
    "스택초기화",
    "스택넣기",
    "스택빼기",
    "스택보기",
    "스택비었나",
    "스택가득찼나",
    "스택크기",
    "큐초기화",
    "큐넣기",
    "큐빼기",
    "큐보기",
    "큐비었나",
    "큐가득찼나",
    "큐크기",
];

const skkoaTypes = new Set([
    "정수",
    "실수",
    "논리",
    "문자",
    "문자열",
    "없음",
    "포인터",
]);

function escapeHtml(value) {
    return value
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;");
}

function findCommentIndex(line) {
    let inString = false;
    let escaped = false;

    for (let i = 0; i < line.length; i++) {
        const char = line[i];
        const next = line[i + 1];

        if (escaped) {
            escaped = false;
            continue;
        }
        if (char === "\\") {
            escaped = true;
            continue;
        }
        if (char === '"') {
            inString = !inString;
            continue;
        }
        if (!inString) {
            if (char === "#" || char === ";") return i;
            if (char === "/" && (next === "/" || next === "*")) return i;
        }
    }

    return -1;
}

function highlightPlainCode(segment) {
    const keywordPattern = new RegExp(
        `(^|[^A-Za-z0-9_가-힣])(${skkoaKeywords.join("|")})(?=$|[^A-Za-z0-9_가-힣])`,
        "g"
    );

    return escapeHtml(segment)
        .replace(/\b(\d+)\b/g, '<span class="skkoa-token number">$1</span>')
        .replace(keywordPattern, function (_, prefix, keyword) {
            const className = skkoaTypes.has(keyword) ? "type" : "keyword";
            return `${prefix}<span class="skkoa-token ${className}">${keyword}</span>`;
        });
}

function highlightCodeSegment(segment) {
    const stringPattern = /"(?:\\.|[^"\\])*"/g;
    let result = "";
    let cursor = 0;
    let match;

    while ((match = stringPattern.exec(segment)) !== null) {
        result += highlightPlainCode(segment.slice(cursor, match.index));
        result += `<span class="skkoa-token string">${escapeHtml(match[0])}</span>`;
        cursor = match.index + match[0].length;
    }

    result += highlightPlainCode(segment.slice(cursor));
    return result;
}

function highlightCodeText(text) {
    return text
        .split("\n")
        .map((line) => {
            const commentIndex = findCommentIndex(line);
            if (commentIndex < 0) return highlightCodeSegment(line);

            const code = line.slice(0, commentIndex);
            const comment = line.slice(commentIndex);
            return (
                highlightCodeSegment(code) +
                `<span class="skkoa-token comment">${escapeHtml(comment)}</span>`
            );
        })
        .join("\n");
}

function highlightDocsCodeBlocks(attempt = 0) {
    const reader = document.getElementById("docReader");
    const root = reader?.shadowRoot;
    const blocks = root ? root.querySelectorAll("pre code") : [];

    if (!blocks.length && attempt < 20) {
        window.setTimeout(() => highlightDocsCodeBlocks(attempt + 1), 60);
        return;
    }

    blocks.forEach((block) => {
        if (block.dataset.skkoaHighlighted === "true") return;
        block.innerHTML = highlightCodeText(block.textContent);
        block.dataset.skkoaHighlighted = "true";
    });
}

function scheduleCodeHighlight() {
    window.requestAnimationFrame(() => highlightDocsCodeBlocks());
}

function selectDoc(id) {
    const doc = docs.find(([key]) => key === id) ? id : "index";
    const reader = document.getElementById("docReader");
    if (reader) {
        reader.setAttribute("src", `text/${doc}.md`);
    }
    document.querySelectorAll(".docs-nav a").forEach((link) => {
        link.classList.toggle("active", link.dataset.doc === doc);
    });
    if (location.hash.slice(1) !== doc) {
        history.replaceState(null, "", `#${doc}`);
    }
    scheduleCodeHighlight();
}

document.addEventListener("DOMContentLoaded", function () {
    const nav = document.getElementById("docsNav");
    if (!nav) return;

    docs.forEach(([id, label]) => {
        const link = document.createElement("a");
        link.href = `#${id}`;
        link.dataset.doc = id;
        link.textContent = label;
        link.addEventListener("click", function (event) {
            event.preventDefault();
            selectDoc(id);
            document.querySelector(".docs-reader")?.scrollIntoView({
                behavior: "smooth",
                block: "start",
            });
        });
        nav.appendChild(link);
    });

    selectDoc(location.hash.slice(1) || "index");

    const reader = document.getElementById("docReader");
    if (reader) {
        reader.addEventListener("zero-md-rendered", scheduleCodeHighlight);
        window.setTimeout(scheduleCodeHighlight, 250);
    }
});

window.addEventListener("hashchange", function () {
    selectDoc(location.hash.slice(1) || "index");
});
