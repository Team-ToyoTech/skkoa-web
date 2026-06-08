const SKKOA_KEYWORDS = [
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

const DEFAULT_CODE = `시작
    출력 "안녕하세요, SKKOA!"
끝`;

const COMPILER_DOWNLOAD_PAGE = "/download/";

const EXAMPLE_FILES = [
    ["hello.koa", "Hello"],
    ["variables.koa", "Variables"],
    ["condition.koa", "Condition"],
    ["loop.koa", "Loop"],
    ["repeat.koa", "Repeat"],
    ["function.koa", "Function"],
    ["function_params.koa", "Function Params"],
    ["array.koa", "Array"],
    ["array_literal.koa", "Array Literal"],
    ["strings.koa", "Strings"],
    ["string_input.koa", "String Input"],
    ["stdlib_strings.koa", "String Library"],
    ["float.koa", "Float"],
    ["char.koa", "Char"],
    ["input.koa", "Input"],
    ["pointer.koa", "Pointer"],
    ["pointer_write.koa", "Pointer Write"],
    ["memory.koa", "Memory"],
    ["struct.koa", "Struct"],
    ["module.koa", "Module"],
    ["stack.koa", "Stack"],
    ["queue.koa", "Queue"],
    ["structures_usage.koa", "Structures"],
];

function escapeHtml(value) {
    return value
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;");
}

let pendingInputResolver = null;

function highlightCodeText(code) {
    const keywordPattern = new RegExp(
        `(^|[^A-Za-z0-9_가-힣])(${SKKOA_KEYWORDS.join("|")})(?=$|[^A-Za-z0-9_가-힣])`,
        "g"
    );

    return escapeHtml(code)
        .replace(/("(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*')/g, '<span class="str">$1</span>')
        .replace(/(#.*|\/\/.*)$/gm, '<span class="comment">$1</span>')
        .replace(keywordPattern, function (match, prefix, keyword) {
            if (
                [
                    "정수",
                    "실수",
                    "논리",
                    "문자",
                    "문자열",
                    "없음",
                    "포인터",
                ].includes(keyword)
            ) {
                return `${prefix}<span class="type">${keyword}</span>`;
            }
            return `${prefix}<span class="kw">${keyword}</span>`;
        })
        .replace(/\b(\d+(?:\.\d+)?)\b/g, '<span class="num">$1</span>');
}

function updateSyntaxPreview() {
    const highlight = document.getElementById("codeHighlight");
    const textarea = document.querySelector(".code-input");
    if (!highlight || !textarea) return;

    const text = textarea.value.endsWith("\n")
        ? textarea.value + " "
        : textarea.value;
    highlight.innerHTML = highlightCodeText(text);
    highlight.scrollTop = textarea.scrollTop;
    highlight.scrollLeft = textarea.scrollLeft;
}

function stripLineComment(line) {
    const hash = line.indexOf("#");
    const slash = line.indexOf("//");
    let cut = -1;
    if (hash >= 0) cut = hash;
    if (slash >= 0 && (cut < 0 || slash < cut)) cut = slash;
    return cut >= 0 ? line.slice(0, cut) : line;
}

function normalizeLines(code) {
    return code
        .split(/\r?\n/)
        .map(stripLineComment)
        .map((line) => line.trim())
        .filter(Boolean);
}

function startsBlock(line) {
    return (
        /^만약\s+.+\s+이면$/.test(line) ||
        /^동안\s+.+\s+반복$/.test(line) ||
        /^반복\s+.+:\s*.+부터\s*.+까지$/.test(line)
    );
}

function findMatchingEnd(lines, start) {
    let depth = 0;
    for (let i = start; i < lines.length; i++) {
        if (startsBlock(lines[i]) || /^함수\s+/.test(lines[i]) || lines[i] === "시작") {
            depth++;
        }
        if (lines[i] === "끝") {
            depth--;
            if (depth === 0) return i;
        }
    }
    return -1;
}

function evalExpression(expr, env) {
    const jsExpr = expr
        .replace(/\b참\b/g, "true")
        .replace(/\b거짓\b/g, "false")
        .replace(/\b그리고\b/g, "&&")
        .replace(/\b또는\b/g, "||")
        .replace(/\b아님\b/g, "!")
        .replace(/주소\(\s*([A-Za-z_가-힣][A-Za-z0-9_가-힣]*)\s*\)/g, '__addr("$1")')
        .replace(/값\(([^)]+)\)/g, "__value($1)")
        .replace(/할당\(([^)]+)\)/g, "__alloc($1)")
        .replace(/해제\(([^)]+)\)/g, "__free($1)");
    return Function("env", `with (env) { return (${jsExpr}); }`)(env);
}

function parseFunctions(lines, env) {
    for (let i = 0; i < lines.length; i++) {
        const match = lines[i].match(/^함수\s+([^\s(]+)\((.*)\):\s*(\S+)$/);
        if (!match) continue;

        const end = findMatchingEnd(lines, i);
        const params = match[2]
            .split(",")
            .map((part) => part.trim())
            .filter(Boolean)
            .map((part) => part.split(":")[0].trim());
        const returnLine = lines
            .slice(i + 1, end)
            .find((line) => line.startsWith("반환 "));
        if (returnLine) {
            const returnExpr = returnLine.replace(/^반환\s+/, "");
            env[match[1]] = (...args) => {
                const localEnv = Object.create(env);
                params.forEach((name, index) => {
                    localEnv[name] = args[index];
                });
                return evalExpression(returnExpr, localEnv);
            };
        }
        i = end;
    }
}

function splitIfSegments(lines, start, end) {
    const firstCondition = lines[start].replace(/^만약\s+/, "").replace(/\s+이면$/, "");
    const segments = [{ condition: firstCondition, start: start + 1, end }];
    let depth = 0;

    for (let i = start + 1; i < end; i++) {
        const line = lines[i];
        if (startsBlock(line)) depth++;
        if (line === "끝") depth--;
        if (depth === 0 && /^아니면만약\s+.+\s+이면$/.test(line)) {
            segments[segments.length - 1].end = i;
            segments.push({
                condition: line.replace(/^아니면만약\s+/, "").replace(/\s+이면$/, ""),
                start: i + 1,
                end,
            });
        } else if (depth === 0 && line === "아니면") {
            segments[segments.length - 1].end = i;
            segments.push({ condition: null, start: i + 1, end });
        }
    }
    return segments;
}

function appendConsole(text) {
    const outputEl = document.getElementById("runOutput");
    if (!outputEl) return;
    outputEl.textContent += text;
    outputEl.scrollTop = outputEl.scrollHeight;
}

function readConsoleInput(promptText) {
    const input = document.getElementById("consoleInput");
    const button = document.getElementById("consoleSubmit");
    appendConsole(`${promptText}> `);
    input.disabled = false;
    button.disabled = false;
    input.focus();

    return new Promise((resolve) => {
        pendingInputResolver = (value) => {
            appendConsole(`${value}\n`);
            pendingInputResolver = null;
            resolve(value);
        };
    });
}

async function executeBlock(lines, start, end, env, output) {
    for (let i = start; i < end; i++) {
        const line = lines[i];

        if (line === "끝" || line === "아니면" || line.startsWith("아니면만약 ")) {
            continue;
        }

        let match = line.match(/^변수\s+([^\s:[\]]+):\s*정수\[(\d+)\]$/);
        if (match) {
            env[match[1]] = new Array(Number(match[2])).fill(0);
            continue;
        }

        match = line.match(/^변수\s+([^\s:]+):\s*(정수|논리|실수|문자|문자열|포인터<[^>]+>)(?:\s*=\s*(.+))?$/);
        if (match) {
            if (match[2] === "문자열") {
                env[match[1]] = match[3] ? evalExpression(match[3], env) : "";
            } else if (match[2] === "문자") {
                const value = match[3] ? evalExpression(match[3], env) : "\0";
                env[match[1]] = typeof value === "string" ? value[0] ?? "" : value;
            } else {
                env[match[1]] = match[3] ? evalExpression(match[3], env) : 0;
            }
            continue;
        }

        match = line.match(/^([^\s[\]]+)\[(.+)\]\s*=\s*(.+)$/);
        if (match) {
            env[match[1]][evalExpression(match[2], env)] = evalExpression(match[3], env);
            continue;
        }

        match = line.match(/^입력\s+([^\s[\]]+)\[(.+)\]$/);
        if (match) {
            const rawValue = await readConsoleInput(
                `입력 ${match[1]}[${evalExpression(match[2], env)}]`
            );
            env[match[1]][evalExpression(match[2], env)] = Number(rawValue);
            continue;
        }

        match = line.match(/^입력\s+([^\s]+)$/);
        if (match) {
            const rawValue = await readConsoleInput(`입력 ${match[1]}`);
            env[match[1]] = isNaN(Number(rawValue)) ? rawValue : Number(rawValue);
            continue;
        }

        match = line.match(/^([^\s=]+)\s*=\s*(.+)$/);
        if (match) {
            env[match[1]] = evalExpression(match[2], env);
            continue;
        }

        match = line.match(/^출력\s+(.+)$/);
        if (match) {
            const expr = match[1];
            const stringMatch = expr.match(/^"(.*)"$/);
            const value = stringMatch ? stringMatch[1] : evalExpression(expr, env);
            output.push(String(value));
            appendConsole(`${String(value)}\n`);
            continue;
        }

        if (/^만약\s+.+\s+이면$/.test(line)) {
            const blockEnd = findMatchingEnd(lines, i);
            const segments = splitIfSegments(lines, i, blockEnd);
            for (const segment of segments) {
                if (segment.condition === null || evalExpression(segment.condition, env)) {
                    await executeBlock(lines, segment.start, segment.end, env, output);
                    break;
                }
            }
            i = blockEnd;
            continue;
        }

        if (/^동안\s+.+\s+반복$/.test(line)) {
            const condition = line.replace(/^동안\s+/, "").replace(/\s+반복$/, "");
            const blockEnd = findMatchingEnd(lines, i);
            let guard = 0;
            while (evalExpression(condition, env)) {
                await executeBlock(lines, i + 1, blockEnd, env, output);
                guard++;
                if (guard > 1000) {
                    throw new Error("반복이 1000회를 넘어 중단했습니다.");
                }
            }
            i = blockEnd;
            continue;
        }

        match = line.match(/^반복\s+([^\s:]+):\s*(.+?)부터\s*(.+?)까지$/);
        if (match) {
            const iterator = match[1];
            const from = Number(evalExpression(match[2], env));
            const to = Number(evalExpression(match[3], env));
            const blockEnd = findMatchingEnd(lines, i);
            for (let value = from; value <= to; value++) {
                env[iterator] = value;
                await executeBlock(lines, i + 1, blockEnd, env, output);
            }
            i = blockEnd;
            continue;
        }

        evalExpression(line, env);
    }
}

async function runSimulation() {
    const outputEl = document.getElementById("runOutput");
    const code = document.querySelector(".code-input").value;
    try {
        outputEl.textContent = "";
        const lines = normalizeLines(code);
        const heap = [];
        const env = {
            __addr(name) {
                return { kind: "var", name };
            },
            __value(pointer) {
                if (!pointer) return 0;
                if (pointer.kind === "var") return env[pointer.name];
                if (pointer.kind === "heap") return heap[pointer.index] ?? 0;
                return 0;
            },
            __alloc(size) {
                const index = heap.length;
                heap.push(0);
                return { kind: "heap", index, size };
            },
            __free(pointer) {
                if (pointer?.kind === "heap") heap[pointer.index] = undefined;
                return 0;
            },
        };
        parseFunctions(lines, env);
        const start = lines.findIndex((line) => line === "시작");
        if (start < 0) throw new Error("'시작' 블록을 찾을 수 없습니다.");
        const end = findMatchingEnd(lines, start);
        if (end < 0) throw new Error("'시작' 블록을 닫는 '끝'이 필요합니다.");
        const output = [];
        await executeBlock(lines, start + 1, end, env, output);
        if (!output.length) appendConsole("출력 없음\n");
    } catch (error) {
        outputEl.textContent = `시뮬레이터 오류: ${error.message}`;
    }
    updateSyntaxPreview();
}

document.getElementById("runButton").addEventListener("click", runSimulation);

document.getElementById("saveButton").addEventListener("click", function () {
    const code = document.querySelector(".code-input").value;
    let filename = document.getElementById("filenameInput").value.trim();
    filename = filename.replace(/\.[^/.]+$/, "");
    filename += ".koa";

    const blob = new Blob([code], { type: "text/plain" });
    const url = URL.createObjectURL(blob);

    const a = document.createElement("a");
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    setTimeout(() => {
        document.body.removeChild(a);
        URL.revokeObjectURL(url);
    }, 0);
});

document.getElementById("stopButton").addEventListener("click", function () {
    const output = document.getElementById("runOutput");
    if (output) output.textContent = "시뮬레이션이 중지되었습니다.";
});

document.getElementById("githubButton").addEventListener("click", function () {
    window.open("https://github.com/meozigoon/skkoa-web-compiler", "_blank");
});

let tabs = [];
let activeTabId = null;
let isDirty = false;

function createTab(filename = "untitled", content = DEFAULT_CODE) {
    filename = filename.replace(/\.[^/.]+$/, "");
    if (!filename.endsWith(".koa")) filename += ".koa";
    const id =
        "tab_" + Date.now() + "_" + Math.random().toString(36).slice(2, 8);
    tabs.push({ id, filename, content });
    setActiveTab(id);
    renderTabs();
}

function setActiveTab(id) {
    activeTabId = id;
    const tab = tabs.find((t) => t.id === id);
    if (tab) {
        document.querySelector(".code-input").value = tab.content;
        let fname = tab.filename.replace(/\.[^/.]+$/, "");
        if (!fname.endsWith(".koa")) fname += ".koa";
        document.getElementById("filenameInput").value = fname;
        updateSyntaxPreview();
        updateLineNumbers();
    }
    renderTabs();
}

function renderTabs() {
    const tabBar = document.getElementById("tabBar");
    tabBar.innerHTML = "";
    const linenumDiv = document.getElementById("editorLinenum");
    if (linenumDiv) {
        const linenumRect = linenumDiv.getBoundingClientRect();
        tabBar.style.marginLeft = linenumRect.width + "px";
    } else {
        tabBar.style.marginLeft = "";
    }
    tabs.forEach((tab) => {
        const tabEl = document.createElement("div");
        tabEl.className = "tab" + (tab.id === activeTabId ? " active" : "");
        const nameSpan = document.createElement("span");
        nameSpan.textContent = tab.filename;
        nameSpan.style.flex = "1";
        if (tab.id === activeTabId) {
            nameSpan.style.cursor = "pointer";
            nameSpan.onclick = (e) => {
                e.stopPropagation();
                const input = document.createElement("input");
                input.type = "text";
                let baseName = tab.filename.replace(/\.[^/.]+$/, "");
                if (baseName.endsWith(".koa")) baseName = baseName.slice(0, -4);
                input.value = baseName;
                input.style.background = "#000";
                input.style.color = "#fff";
                input.style.border = "none";
                input.style.outline = "none";
                input.style.fontFamily = "inherit";
                input.style.fontSize = "15px";
                input.style.padding = "2px 6px";
                input.style.borderRadius = "4px";
                input.style.width =
                    Math.max(60, tab.filename.length * 9) + "px";
                nameSpan.replaceWith(input);
                input.focus();
                input.select();
                input.onblur = input.onkeydown = function (ev) {
                    if (ev.type === "blur" || ev.key === "Enter") {
                        let newName = input.value.trim() || "untitled";
                        let base = newName,
                            num = 1;
                        let finalName = base;
                        while (
                            tabs.some(
                                (t) =>
                                    t.filename === finalName + ".koa" &&
                                    t.id !== tab.id
                            )
                        ) {
                            finalName = base + "_" + num;
                            num++;
                        }
                        tab.filename = finalName + ".koa";
                        document.getElementById("filenameInput").value =
                            finalName + ".koa";
                        renderTabs();
                    } else if (ev.key === "Escape") {
                        renderTabs();
                    }
                };
            };
        }
        tabEl.appendChild(nameSpan);
        const closeBtn = document.createElement("button");
        closeBtn.className = "close-btn";
        closeBtn.innerHTML = "&times;";
        closeBtn.onclick = (e) => {
            e.stopPropagation();
            closeTab(tab.id);
        };
        tabEl.appendChild(closeBtn);
        tabEl.onclick = () => setActiveTab(tab.id);
        tabBar.appendChild(tabEl);
    });
    let addBtn = document.getElementById("tabAddBtn");
    if (!addBtn) {
        addBtn = document.createElement("button");
        addBtn.id = "tabAddBtn";
        addBtn.className = "tab-add-btn";
        addBtn.title = "New Tab";
        addBtn.textContent = "+";
        addBtn.onclick = function () {
            let untitledNum = 1;
            let name;
            do {
                name = "untitled" + untitledNum + ".koa";
                untitledNum++;
            } while (tabs.some((t) => t.filename === name));
            createTab(name, "");
        };
    }
    tabBar.appendChild(addBtn);
}

function closeTab(id) {
    const idx = tabs.findIndex((t) => t.id === id);
    if (idx !== -1) {
        tabs.splice(idx, 1);
        if (activeTabId === id) {
            if (tabs.length > 0) {
                setActiveTab(tabs[Math.max(0, idx - 1)].id);
            } else {
                createTab("untitled1", "");
            }
        } else {
            renderTabs();
        }
    }
}

function updateLineNumbers() {
    const textarea = document.querySelector(".code-input");
    const linenumLayer = document.getElementById("editorLinenum");
    if (!textarea) return;
    if (!linenumLayer) return;
    const lines = textarea.value.split("\n").length;
    let nums = "";
    let activeLine = 1;
    if (typeof textarea.selectionStart === "number") {
        const uptoCursor = textarea.value.slice(0, textarea.selectionStart);
        activeLine = uptoCursor.split("\n").length;
    }
    for (let i = 1; i <= lines; i++) {
        let lineClass = "";
        if (i === activeLine)
            lineClass = "active-linenum active-linenum-border";
        nums += `<div class="${lineClass}">${i}</div>`;
    }
    linenumLayer.innerHTML = nums;
    linenumLayer.scrollTop = textarea.scrollTop;
}

function applyEditorFontSize(size) {
    const normalized = typeof size === "number" ? `${size}px` : size;
    const editorRoot = document.querySelector(".editor-linenum-wrap");
    const codeInput = document.querySelector(".code-input");
    const codeHighlight = document.getElementById("codeHighlight");
    const linenumLayer = document.getElementById("editorLinenum");

    if (editorRoot) editorRoot.style.setProperty("--editor-font-size", normalized);
    if (codeInput) codeInput.style.fontSize = normalized;
    if (codeHighlight) codeHighlight.style.fontSize = normalized;
    if (linenumLayer) linenumLayer.style.fontSize = normalized;

    updateSyntaxPreview();
    updateLineNumbers();
}

function openCompilerDownloadPage() {
    window.open(COMPILER_DOWNLOAD_PAGE, "_blank");
}

function populateExampleSelect() {
    const select = document.getElementById("exampleSelect");
    if (!select) return;

    EXAMPLE_FILES.forEach(([filename, label]) => {
        const option = document.createElement("option");
        option.value = filename;
        option.textContent = label;
        select.appendChild(option);
    });
}

async function loadExample(filename) {
    if (!filename) return;

    const output = document.getElementById("runOutput");
    try {
        const response = await fetch(`examples/${encodeURIComponent(filename)}`);
        if (!response.ok) {
            throw new Error(`${filename} 파일을 불러올 수 없습니다.`);
        }
        const content = await response.text();
        createTab(filename, content);
        if (output) output.textContent = `예제를 불러왔습니다: ${filename}`;
    } catch (error) {
        if (output) output.textContent = `예제 불러오기 오류: ${error.message}`;
    }
}

function openExamplePicker() {
    const select = document.getElementById("exampleSelect");
    if (!select) return;

    select.focus();
    if (typeof select.showPicker === "function") {
        select.showPicker();
    }
}

const codeInput = document.querySelector(".code-input");
codeInput.addEventListener("input", () => {
    const tab = tabs.find((t) => t.id === activeTabId);
    if (tab) tab.content = codeInput.value;
    updateLineNumbers();
    updateSyntaxPreview();
    isDirty = true;
});
codeInput.addEventListener("scroll", function () {
    const linenumLayer = document.getElementById("editorLinenum");
    const highlight = document.getElementById("codeHighlight");
    if (linenumLayer) linenumLayer.scrollTop = codeInput.scrollTop;
    if (highlight) {
        highlight.scrollTop = codeInput.scrollTop;
        highlight.scrollLeft = codeInput.scrollLeft;
    }
});
codeInput.addEventListener("click", updateLineNumbers);
codeInput.addEventListener("keyup", updateLineNumbers);
codeInput.addEventListener("select", updateLineNumbers);
filenameInput.addEventListener("input", () => {
    const tab = tabs.find((t) => t.id === activeTabId);
    if (tab) {
        let fname = filenameInput.value.trim().replace(/\.[^/.]+$/, "");
        if (!fname.endsWith(".koa")) fname += ".koa";
        tab.filename = fname;
        document.getElementById("filenameInput").value = fname;
    }
    renderTabs();
    isDirty = true;
});

const openFileMenu = document.getElementById("openFileMenu");
openFileMenu.addEventListener("click", openFileHandler);

const openFileToolbarBtn = document.getElementById("openFileToolbarBtn");
if (openFileToolbarBtn)
    openFileToolbarBtn.addEventListener("click", openFileHandler);

function openFileHandler() {
    const input = document.createElement("input");
    input.type = "file";
    input.accept = ".koa";
    input.style.display = "none";
    document.body.appendChild(input);
    input.addEventListener("change", function (e) {
        const file = input.files[0];
        if (!file) return;
        const reader = new FileReader();
        reader.onload = function (evt) {
            let fname = file.name.replace(/\.[^/.]+$/, "");
            if (!fname.endsWith(".koa")) fname += ".koa";
            createTab(fname, evt.target.result);
        };
        reader.readAsText(file);
    });
    input.click();
    setTimeout(() => document.body.removeChild(input), 1000);
}

const newFileMenu = document.getElementById("newFileMenu");
newFileMenu.addEventListener("click", function () {
    let untitledNum = 1;
    let name;
    do {
        name = "untitled" + untitledNum + ".koa";
        untitledNum++;
    } while (tabs.some((t) => t.filename === name));
    createTab(name, "");
});

const tabAddBtn = document.getElementById("tabAddBtn");
tabAddBtn.addEventListener("click", function () {
    let untitledNum = 1;
    let name;
    do {
        name = "untitled" + untitledNum + ".koa";
        untitledNum++;
    } while (tabs.some((t) => t.filename === name));
    createTab(name, "");
});

document.getElementById("menuToggleBtn").addEventListener("click", function () {
    const sideMenu = document.querySelector(".side-menu");
    const editorArea = document.querySelector(".editor-area");
    sideMenu.classList.toggle("open");
    if (sideMenu.classList.contains("open")) {
        editorArea.style.marginLeft = "210px";
    } else {
        editorArea.style.marginLeft = "0";
    }
});

const settingsMenu = document.getElementById("settingsMenu");
const settingsModal = document.getElementById("settingsModal");
let settingsCloseBtn = document.getElementById("settingsCloseBtn");
if (settingsCloseBtn) settingsCloseBtn.remove();

const closeX = document.createElement("button");
closeX.innerHTML = "&times;";
closeX.setAttribute("aria-label", "Close");
closeX.style.position = "absolute";
closeX.style.top = "16px";
closeX.style.right = "20px";
closeX.style.background = "none";
closeX.style.border = "none";
closeX.style.color = "#fff";
closeX.style.fontSize = "2rem";
closeX.style.cursor = "pointer";
closeX.style.zIndex = "10";
closeX.id = "settingsModalCloseX";
settingsModal.appendChild(closeX);
closeX.addEventListener("click", function () {
    settingsModal.style.display = "none";
});

const bgRadios = document.getElementsByName("bgcolor");
const fontRadios = document.getElementsByName("fontsize");
const fontsizeInput = document.getElementById("fontsizeInput");
const fontsizeToggleBtn = document.getElementById("fontsizeToggleBtn");
let tabSize = 4;

function indentUnit() {
    return " ".repeat(tabSize);
}

function getLineBounds(value, position) {
    const lineStart = value.lastIndexOf("\n", Math.max(0, position - 1)) + 1;
    const nextNewline = value.indexOf("\n", position);
    return {
        start: lineStart,
        end: nextNewline === -1 ? value.length : nextNewline,
    };
}

function getLeadingWhitespace(line) {
    return line.match(/^[ \t]*/)?.[0] ?? "";
}

function removeOneIndentLevel(indent) {
    if (!indent) return "";
    if (indent.endsWith("\t")) return indent.slice(0, -1);

    const unit = indentUnit();
    if (indent.endsWith(unit)) return indent.slice(0, -unit.length);

    const removeCount = Math.min(tabSize, indent.length);
    return indent.slice(0, indent.length - removeCount);
}

function getIndentRelevantText(line) {
    return stripLineComment(line).trim();
}

function isBlockOpeningLine(trimmedLine) {
    return (
        trimmedLine === "시작" ||
        /^구조체\s+[A-Za-z_가-힣][A-Za-z0-9_가-힣]*$/.test(trimmedLine) ||
        /^함수\s+.+\)\s*:\s*\S+$/.test(trimmedLine) ||
        /^만약\s+.+\s+이면$/.test(trimmedLine) ||
        /^아니면만약\s+.+\s+이면$/.test(trimmedLine) ||
        trimmedLine === "아니면" ||
        /^동안\s+.+\s+반복$/.test(trimmedLine) ||
        /^반복\s+.+:\s*.+부터\s*.+까지$/.test(trimmedLine)
    );
}

function isBlockContinuationLine(trimmedLine) {
    return (
        trimmedLine === "아니면" ||
        /^아니면만약\s+.+\s+이면$/.test(trimmedLine)
    );
}

function isBlockClosingLine(trimmedLine) {
    return trimmedLine === "끝" || isBlockContinuationLine(trimmedLine);
}

function syncEditorAfterProgrammaticEdit(textarea) {
    const tab = tabs.find((t) => t.id === activeTabId);
    if (tab) tab.content = textarea.value;
    updateLineNumbers();
    updateSyntaxPreview();
    isDirty = true;
}

const tabsizeInput = document.getElementById("tabsizeInput");
const tabsizeToggleBtn = document.getElementById("tabsizeToggleBtn");
if (tabsizeInput) {
    tabsizeInput.value = tabSize;
    function applyTabSize() {
        let val = parseInt(tabsizeInput.value, 10);
        if (isNaN(val) || val < 1 || val > 8) val = 4;
        tabSize = val;
        tabsizeInput.value = tabSize;
    }
    tabsizeInput.addEventListener("change", applyTabSize);
    if (tabsizeToggleBtn)
        tabsizeToggleBtn.addEventListener("click", applyTabSize);
    tabsizeInput.addEventListener("keydown", function (e) {
        if (e.key === "Enter") applyTabSize();
    });
}

settingsMenu.addEventListener("click", function () {
    settingsModal.style.display = "flex";
    const body = document.body;
    let val = "default";
    if (body.classList.contains("bg-white")) val = "white";
    else if (body.classList.contains("bg-purple")) val = "purple";
    for (const r of bgRadios) r.checked = r.value === val;

    const codeInput = document.querySelector(".code-input");
    let curFont = codeInput ? codeInput.style.fontSize.replace("px", "") : "14";
    if (!curFont)
        curFont = window.getComputedStyle(codeInput).fontSize.replace("px", "");
    fontsizeInput.value = curFont;
});
settingsModal.addEventListener("click", function (e) {
    if (e.target === settingsModal) settingsModal.style.display = "none";
});

for (const radio of bgRadios) {
    radio.addEventListener("change", function () {
        document.body.classList.remove("bg-white", "bg-purple");
        if (this.value === "white") document.body.classList.add("bg-white");
        else if (this.value === "purple")
            document.body.classList.add("bg-purple");
    });
}

for (const radio of fontRadios) {
    radio.addEventListener("change", function () {
        applyEditorFontSize(`${this.value}px`);
    });
}

fontsizeToggleBtn.addEventListener("click", function () {
    let size = parseInt(fontsizeInput.value, 10);
    if (isNaN(size) || size < 10) size = 10;
    if (size > 32) size = 32;
    fontsizeInput.value = size;
    applyEditorFontSize(size);
});
fontsizeInput.addEventListener("keydown", function (e) {
    if (e.key === "Enter") fontsizeToggleBtn.click();
});

window.addEventListener("DOMContentLoaded", () => {
    populateExampleSelect();
    updateLineNumbers();
    if (tabs.length === 0) createTab("untitled1");
});

window.addEventListener("beforeunload", function (e) {
    if (isDirty) {
        e.preventDefault();
    }
});

codeInput.addEventListener("keydown", function (e) {
    if (e.key === "Tab") {
        e.preventDefault();
        const start = this.selectionStart;
        const end = this.selectionEnd;
        const value = this.value;
        const spaces = indentUnit();
        this.value = value.substring(0, start) + spaces + value.substring(end);
        this.selectionStart = this.selectionEnd = start + tabSize;
        syncEditorAfterProgrammaticEdit(this);
        return;
    }

    if (e.key === "Enter" && !e.isComposing) {
        e.preventDefault();

        let start = this.selectionStart;
        let end = this.selectionEnd;
        let value = this.value;
        const bounds = getLineBounds(value, start);
        const currentLine = value.slice(bounds.start, bounds.end);
        const beforeCursor = value.slice(bounds.start, start);
        const afterCursor = value.slice(end, bounds.end);
        let currentIndent = getLeadingWhitespace(currentLine);
        const trimmedBeforeCursor = getIndentRelevantText(beforeCursor);
        const trimmedFullLine = getIndentRelevantText(currentLine);
        const cursorAtLogicalLineEnd = afterCursor.trim().length === 0;

        if (
            cursorAtLogicalLineEnd &&
            trimmedBeforeCursor === trimmedFullLine &&
            isBlockClosingLine(trimmedBeforeCursor)
        ) {
            const reducedIndent = removeOneIndentLevel(currentIndent);
            if (reducedIndent !== currentIndent) {
                value =
                    value.slice(0, bounds.start) +
                    reducedIndent +
                    value.slice(bounds.start + currentIndent.length);
                const delta = reducedIndent.length - currentIndent.length;
                start += delta;
                end += delta;
                currentIndent = reducedIndent;
            }
        }

        let nextIndent = currentIndent;
        if (isBlockOpeningLine(trimmedBeforeCursor)) {
            nextIndent += indentUnit();
        }

        const insertion = "\n" + nextIndent;
        this.value = value.slice(0, start) + insertion + value.slice(end);
        this.selectionStart = this.selectionEnd = start + insertion.length;
        syncEditorAfterProgrammaticEdit(this);
    }
});

function submitConsoleInput() {
    const input = document.getElementById("consoleInput");
    if (!pendingInputResolver || !input) return;
    const value = input.value;
    input.value = "";
    pendingInputResolver(value);
}

document
    .getElementById("consoleSubmit")
    .addEventListener("click", submitConsoleInput);
document.getElementById("consoleInput").addEventListener("keydown", function (e) {
    if (e.key === "Enter") {
        e.preventDefault();
        submitConsoleInput();
    }
});

document
    .getElementById("settingsToolbarBtn")
    .addEventListener("click", function () {
        document.getElementById("settingsModal").style.display = "flex";
    });

document.getElementById("runMenu").addEventListener("click", runSimulation);
document.getElementById("stopMenu").addEventListener("click", function () {
    document.getElementById("stopButton").click();
});
document.getElementById("saveMenu").addEventListener("click", function () {
    document.getElementById("saveButton").click();
});
document.getElementById("downloadMenu").addEventListener("click", function () {
    openCompilerDownloadPage();
});
document.getElementById("examplesMenu").addEventListener("click", openExamplePicker);
document.getElementById("exampleSelect").addEventListener("change", function () {
    const filename = this.value;
    this.value = "";
    loadExample(filename);
});
document.getElementById("studyMenu").addEventListener("click", function () {
    window.open("/docs/", "_blank");
});
