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
    "중단",
    "계속",
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
    "배열길이",
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
    출력 "안녕하세요, SKKOA; LTW!"
끝`;

const COMPILER_DOWNLOAD_PAGE = "/download/";

const EXAMPLE_FILES = [
    ["hello.koa", "Hello"],
    ["variables.koa", "Variables"],
    ["condition.koa", "Condition"],
    ["loop.koa", "Loop"],
    ["repeat.koa", "Repeat"],
    ["break_continue.koa", "Break / Continue"],
    ["function.koa", "Function"],
    ["function_params.koa", "Function Params"],
    ["many_params.koa", "Many Params"],
    ["array.koa", "Array"],
    ["array_literal.koa", "Array Literal"],
    ["array_length.koa", "Array Length"],
    ["strings.koa", "Strings"],
    ["string_input.koa", "String Input"],
    ["batch_input.koa", "Batch Input"],
    ["stdlib_strings.koa", "String Library"],
    ["float.koa", "Float"],
    ["char.koa", "Char"],
    ["input.koa", "Input"],
    ["pointer.koa", "Pointer"],
    ["pointer_write.koa", "Pointer Write"],
    ["memory.koa", "Memory"],
    ["struct.koa", "Struct"],
    ["struct_copy.koa", "Struct Copy"],
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

function byId(id) {
    return document.getElementById(id);
}

function addEvent(idOrElement, type, handler, options) {
    const element =
        typeof idOrElement === "string" ? byId(idOrElement) : idOrElement;
    if (element) element.addEventListener(type, handler, options);
}

let pendingInputResolver = null;
let consoleInputBuffer = "";

function highlightCodeText(code) {
    const keywordPattern = new RegExp(
        `(^|[^A-Za-z0-9_가-힣])(${SKKOA_KEYWORDS.join("|")})(?=$|[^A-Za-z0-9_가-힣])`,
        "g",
    );

    return escapeHtml(code)
        .replace(
            /("(?:\\.|[^"\\])*"|'(?:\\.|[^'\\])*')/g,
            '<span class="str">$1</span>',
        )
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
    let quote = null;
    let escaped = false;
    for (let i = 0; i < line.length; i++) {
        const ch = line[i];
        if (escaped) {
            escaped = false;
            continue;
        }
        if (quote) {
            if (ch === "\\") {
                escaped = true;
            } else if (ch === quote) {
                quote = null;
            }
            continue;
        }
        if (ch === '"' || ch === "'") {
            quote = ch;
            continue;
        }
        if (ch === "#") return line.slice(0, i);
        if (ch === "/" && line[i + 1] === "/") return line.slice(0, i);
    }
    return line;
}

function normalizeLines(code) {
    return code
        .split(/\r?\n/)
        .map(stripLineComment)
        .map((line) => line.trim())
        .filter(Boolean);
}

function normalizeImportPath(path) {
    const normalized = String(path || "").trim().replace(/\\/g, "/");
    if (
        !normalized ||
        normalized.startsWith("/") ||
        /^[a-z][a-z0-9+.-]*:/i.test(normalized) ||
        normalized.split("/").includes("..")
    ) {
        throw new Error(`허용되지 않는 가져오기 경로입니다: ${path}`);
    }
    return normalized.endsWith(".koa") ? normalized : `${normalized}.koa`;
}

function encodeImportPath(path) {
    return path
        .split("/")
        .map((part) => encodeURIComponent(part))
        .join("/");
}

async function fetchImportSource(path) {
    const normalized = normalizeImportPath(path);
    const encoded = encodeImportPath(normalized);
    const candidates = [`lib/${encoded}`, `examples/${encoded}`];

    for (const candidate of candidates) {
        try {
            const response = await fetch(candidate);
            if (response.ok) return response.text();
        } catch {
        }
    }

    throw new Error(`가져오기 파일을 찾을 수 없습니다: ${normalized}`);
}

async function expandImports(code, seen = new Set(), active = new Set()) {
    const lines = code.split(/\r?\n/);
    const expanded = [];

    for (const line of lines) {
        const match = stripLineComment(line)
            .trim()
            .match(/^가져오기\s+"([^"]+)"$/);
        if (!match) {
            expanded.push(line);
            continue;
        }

        const importPath = normalizeImportPath(match[1]);
        if (seen.has(importPath)) continue;
        if (active.has(importPath)) {
            throw new Error(`순환 가져오기를 발견했습니다: ${importPath}`);
        }

        seen.add(importPath);
        active.add(importPath);
        try {
            const source = await fetchImportSource(importPath);
            expanded.push(await expandImports(source, seen, active));
        } finally {
            active.delete(importPath);
        }
    }

    return expanded.join("\n");
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
        if (
            startsBlock(lines[i]) ||
            /^함수\s+/.test(lines[i]) ||
            lines[i] === "시작"
        ) {
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
        .replace(
            /주소\(\s*([A-Za-z_가-힣][A-Za-z0-9_가-힣]*)\s*\)/g,
            '__addr("$1")',
        )
        .replace(/값\(([^)]+)\)/g, "__value($1)")
        .replace(/할당\(([^)]+)\)/g, "__alloc($1)")
        .replace(/해제\(([^)]+)\)/g, "__free($1)")
        .replace(
            /배열길이\(\s*([A-Za-z_가-힣][A-Za-z0-9_가-힣]*)\s*\)/g,
            '__arrayLength("$1")',
        );
    return Function("env", `with (env) { return (${jsExpr}); }`)(env);
}

function parseFunctions(lines, env) {
    for (let i = 0; i < lines.length; i++) {
        const match = lines[i].match(/^함수\s+([^\s(]+)\((.*)\):\s*(\S+)$/);
        if (!match) continue;

        const end = findMatchingEnd(lines, i);
        if (end < 0) continue;
        const params = match[2]
            .split(",")
            .map((part) => part.trim())
            .filter(Boolean)
            .map((part) => part.split(":")[0].trim());
        env[match[1]] = (...args) => {
            const localEnv = Object.create(env);
            localEnv.__types = Object.create(env.__types || null);
            params.forEach((name, index) => {
                localEnv[name] = args[index];
            });
            const result = executeFunctionLines(lines, i + 1, end, localEnv);
            return result.returned ? result.value : 0;
        };
        i = end;
    }
}

function defaultValueForType(type) {
    if (type === "문자열") return "";
    if (type === "문자") return "\0";
    if (type === "논리") return false;
    return 0;
}

function cloneSimValue(value) {
    if (Array.isArray(value)) return value.slice();
    if (value && typeof value === "object") {
        if (value.kind === "var" || value.kind === "heap") return { ...value };
        return { ...value };
    }
    return value;
}

function formatSimValue(value) {
    if (value && typeof value === "object") {
        if (value.kind === "var") return `주소(${value.name})`;
        if (value.kind === "heap") return `할당(${value.size})`;
    }
    return String(value);
}

function emitSimOutput(env, value) {
    const formatted = formatSimValue(value);
    if (Array.isArray(env.__output)) env.__output.push(formatted);
    appendConsole(`${formatted}\n`);
}

function setPointerValue(pointer, value, env) {
    if (!pointer) return;
    if (pointer.kind === "var") {
        env[pointer.name] = value;
    } else if (pointer.kind === "heap" && Array.isArray(env.__heap)) {
        env.__heap[pointer.index] = value;
    }
}

function assignSimTarget(target, value, env) {
    let match = target.match(/^([A-Za-z_가-힣][A-Za-z0-9_가-힣]*)\[(.+)\]$/);
    if (match) {
        env[match[1]][evalExpression(match[2], env)] = value;
        return true;
    }

    match = target.match(
        /^([A-Za-z_가-힣][A-Za-z0-9_가-힣]*)\.([A-Za-z_가-힣][A-Za-z0-9_가-힣]*)$/,
    );
    if (match) {
        if (!env[match[1]] || typeof env[match[1]] !== "object") {
            env[match[1]] = {};
        }
        env[match[1]][match[2]] = value;
        return true;
    }

    env[target] = cloneSimValue(value);
    return true;
}

function handleDeclaration(line, env) {
    const match = line.match(
        /^(변수|상수)\s+([A-Za-z_가-힣][A-Za-z0-9_가-힣]*):\s*([^\s=]+)(?:\s*=\s*(.+))?$/,
    );
    if (!match) return false;

    const name = match[2];
    const type = match[3];
    const initializer = match[4];
    const arrayType = type.match(/^(.+)\[(\d*)\]$/);
    if (arrayType) {
        if (env.__types)
            env.__types[name] = { base: arrayType[1], isArray: true };
        if (initializer) {
            const value = evalExpression(initializer, env);
            env[name] = Array.isArray(value) ? value.slice() : [];
        } else {
            const size = Number(arrayType[2] || 0);
            env[name] = new Array(size).fill(defaultValueForType(arrayType[1]));
        }
        return true;
    }

    if (env.__types) env.__types[name] = { base: type, isArray: false };
    if (initializer) {
        env[name] = cloneSimValue(evalExpression(initializer, env));
    } else if (["정수", "논리", "실수", "문자", "문자열"].includes(type)) {
        env[name] = defaultValueForType(type);
    } else if (type.startsWith("포인터<")) {
        env[name] = null;
    } else {
        env[name] = {};
    }
    return true;
}

function handleAssignment(line, env) {
    let match = line.match(/^값\(([^)]+)\)\s*=\s*(.+)$/);
    if (match) {
        setPointerValue(
            evalExpression(match[1], env),
            evalExpression(match[2], env),
            env,
        );
        return true;
    }

    match = line.match(/^([^\s=]+)\s*=\s*(.+)$/);
    if (!match) return false;
    assignSimTarget(match[1], evalExpression(match[2], env), env);
    return true;
}

function executeSimpleLine(line, env) {
    if (handleDeclaration(line, env)) return true;

    let match = line.match(/^출력\s+(.+)$/);
    if (match) {
        const expr = match[1];
        const stringMatch = expr.match(/^"(.*)"$/);
        const value = stringMatch ? stringMatch[1] : evalExpression(expr, env);
        emitSimOutput(env, value);
        return true;
    }

    if (handleAssignment(line, env)) return true;

    evalExpression(line, env);
    return true;
}

function executeFunctionLines(lines, start, end, env) {
    for (let i = start; i < end; i++) {
        const line = lines[i];
        if (
            line === "끝" ||
            line === "아니면" ||
            line.startsWith("아니면만약 ")
        ) {
            continue;
        }

        if (/^만약\s+.+\s+이면$/.test(line)) {
            const blockEnd = findMatchingEnd(lines, i);
            const segments = splitIfSegments(lines, i, blockEnd);
            for (const segment of segments) {
                if (
                    segment.condition === null ||
                    evalExpression(segment.condition, env)
                ) {
                    const result = executeFunctionLines(
                        lines,
                        segment.start,
                        segment.end,
                        env,
                    );
                    if (result.returned) return result;
                    break;
                }
            }
            i = blockEnd;
            continue;
        }

        if (line.startsWith("반환 ")) {
            return {
                returned: true,
                value: evalExpression(line.replace(/^반환\s+/, ""), env),
            };
        }

        executeSimpleLine(line, env);
    }

    return { returned: false, value: 0 };
}

function splitIfSegments(lines, start, end) {
    const firstCondition = lines[start]
        .replace(/^만약\s+/, "")
        .replace(/\s+이면$/, "");
    const segments = [{ condition: firstCondition, start: start + 1, end }];
    let depth = 0;

    for (let i = start + 1; i < end; i++) {
        const line = lines[i];
        if (startsBlock(line)) depth++;
        if (line === "끝") depth--;
        if (depth === 0 && /^아니면만약\s+.+\s+이면$/.test(line)) {
            segments[segments.length - 1].end = i;
            segments.push({
                condition: line
                    .replace(/^아니면만약\s+/, "")
                    .replace(/\s+이면$/, ""),
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

function queueConsoleInput(value) {
    if (typeof value === "string" && value.length > 0) {
        consoleInputBuffer += value;
    }
}

function takeConsoleToken() {
    const firstValueIndex = consoleInputBuffer.search(/\S/);
    if (firstValueIndex < 0) {
        consoleInputBuffer = "";
        return null;
    }

    consoleInputBuffer = consoleInputBuffer.slice(firstValueIndex);
    const match = consoleInputBuffer.match(/^\S+/);
    if (!match) return null;

    consoleInputBuffer = consoleInputBuffer.slice(match[0].length);
    return match[0];
}

function takeConsoleLine() {
    const firstValueIndex = consoleInputBuffer.search(/\S/);
    if (firstValueIndex < 0) {
        consoleInputBuffer = "";
        return null;
    }

    consoleInputBuffer = consoleInputBuffer.slice(firstValueIndex);
    const newline = consoleInputBuffer.match(/\r?\n/);
    if (!newline) {
        const value = consoleInputBuffer;
        consoleInputBuffer = "";
        return value;
    }

    const value = consoleInputBuffer.slice(0, newline.index);
    consoleInputBuffer = consoleInputBuffer.slice(
        newline.index + newline[0].length
    );
    return value;
}

function takeConsoleInput(mode) {
    return mode === "line" ? takeConsoleLine() : takeConsoleToken();
}

function simTypeForInputTarget(env, name) {
    const typeInfo = env.__types && env.__types[name];
    return typeInfo ? typeInfo.base : "";
}

function simInputMode(type) {
    return type === "문자열" ? "line" : "token";
}

function parseSimInputValue(rawValue, type) {
    if (type === "문자열") return rawValue;
    if (type === "문자") return String(rawValue || "\0")[0];
    if (type === "논리") {
        if (rawValue === "참" || rawValue === "true") return true;
        if (rawValue === "거짓" || rawValue === "false") return false;
        return Number(rawValue) !== 0;
    }

    const numeric = Number(rawValue);
    return Number.isNaN(numeric) ? rawValue : numeric;
}

function readConsoleInput(promptText, mode = "token") {
    const input = byId("consoleInput");
    const button = byId("consoleSubmit");
    if (!input || !button) {
        throw new Error("콘솔 입력 UI를 찾을 수 없습니다.");
    }
    appendConsole(`${promptText}> `);

    if (input.value) {
        queueConsoleInput(input.value);
        input.value = "";
    }

    const queuedValue = takeConsoleInput(mode);
    if (queuedValue !== null) {
        appendConsole(`${queuedValue}\n`);
        return Promise.resolve(queuedValue);
    }

    input.disabled = false;
    button.disabled = false;
    input.focus();

    return new Promise((resolve) => {
        pendingInputResolver = (value) => {
            queueConsoleInput(value);
            const resolvedValue = takeConsoleInput(mode);
            if (resolvedValue === null) {
                input.disabled = false;
                button.disabled = false;
                input.focus();
                return;
            }

            appendConsole(`${resolvedValue}\n`);
            pendingInputResolver = null;
            resolve(resolvedValue);
        };
    });
}

async function executeBlock(lines, start, end, env, output) {
    for (let i = start; i < end; i++) {
        const line = lines[i];

        if (
            line === "끝" ||
            line === "아니면" ||
            line.startsWith("아니면만약 ")
        ) {
            continue;
        }

        if (line === "중단") {
            throw { kind: "break" };
        }

        if (line === "계속") {
            throw { kind: "continue" };
        }

        if (handleDeclaration(line, env)) {
            continue;
        }

        if (handleAssignment(line, env)) {
            continue;
        }

        let match = line.match(/^입력\s+([^\s[\]]+)\[(.+)\]$/);
        if (match) {
            const inputType = simTypeForInputTarget(env, match[1]);
            const rawValue = await readConsoleInput(
                `입력 ${match[1]}[${evalExpression(match[2], env)}]`,
                simInputMode(inputType),
            );
            env[match[1]][evalExpression(match[2], env)] = parseSimInputValue(
                rawValue,
                inputType,
            );
            continue;
        }

        match = line.match(/^입력\s+([^\s]+)$/);
        if (match) {
            const inputType = simTypeForInputTarget(env, match[1]);
            const rawValue = await readConsoleInput(
                `입력 ${match[1]}`,
                simInputMode(inputType),
            );
            env[match[1]] = parseSimInputValue(rawValue, inputType);
            continue;
        }

        match = line.match(/^출력\s+(.+)$/);
        if (match) {
            const expr = match[1];
            const stringMatch = expr.match(/^"(.*)"$/);
            const value = stringMatch
                ? stringMatch[1]
                : evalExpression(expr, env);
            emitSimOutput(env, value);
            continue;
        }

        if (/^만약\s+.+\s+이면$/.test(line)) {
            const blockEnd = findMatchingEnd(lines, i);
            const segments = splitIfSegments(lines, i, blockEnd);
            for (const segment of segments) {
                if (
                    segment.condition === null ||
                    evalExpression(segment.condition, env)
                ) {
                    await executeBlock(
                        lines,
                        segment.start,
                        segment.end,
                        env,
                        output,
                    );
                    break;
                }
            }
            i = blockEnd;
            continue;
        }

        if (/^동안\s+.+\s+반복$/.test(line)) {
            const condition = line
                .replace(/^동안\s+/, "")
                .replace(/\s+반복$/, "");
            const blockEnd = findMatchingEnd(lines, i);
            let guard = 0;
            while (evalExpression(condition, env)) {
                try {
                    await executeBlock(lines, i + 1, blockEnd, env, output);
                } catch (signal) {
                    if (signal?.kind === "break") break;
                    if (signal?.kind !== "continue") throw signal;
                }
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
                try {
                    await executeBlock(lines, i + 1, blockEnd, env, output);
                } catch (signal) {
                    if (signal?.kind === "break") break;
                    if (signal?.kind !== "continue") throw signal;
                }
            }
            i = blockEnd;
            continue;
        }

        evalExpression(line, env);
    }
}

async function runSimulation() {
    const outputEl = byId("runOutput");
    const inputEl = document.querySelector(".code-input");
    if (!outputEl || !inputEl) return;
    const code = inputEl.value;
    try {
        outputEl.textContent = "";
        pendingInputResolver = null;
        consoleInputBuffer = "";
        const lines = normalizeLines(await expandImports(code));
        const heap = [];
        const output = [];
        const env = {
            __heap: heap,
            __output: output,
            __types: Object.create(null),
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
            __arrayLength(name) {
                return Array.isArray(env[name]) ? env[name].length : 0;
            },
            길이(value) {
                return String(value).length;
            },
            비교(a, b) {
                const left = String(a);
                const right = String(b);
                if (left === right) return 0;
                return left < right ? -1 : 1;
            },
            부분문자열(value, start, length) {
                return String(value).substring(
                    Number(start),
                    Number(start) + Number(length),
                );
            },
        };
        parseFunctions(lines, env);
        const start = lines.findIndex((line) => line === "시작");
        if (start < 0) throw new Error("'시작' 블록을 찾을 수 없습니다.");
        const end = findMatchingEnd(lines, start);
        if (end < 0) throw new Error("'시작' 블록을 닫는 '끝'이 필요합니다.");
        await executeBlock(lines, start + 1, end, env, output);
        if (!output.length) appendConsole("출력 없음\n");
    } catch (error) {
        if (error?.kind === "break") {
            outputEl.textContent =
                "시뮬레이터 오류: '중단'은 반복문 안에서만 사용할 수 있습니다.";
        } else if (error?.kind === "continue") {
            outputEl.textContent =
                "시뮬레이터 오류: '계속'은 반복문 안에서만 사용할 수 있습니다.";
        } else {
            outputEl.textContent = `시뮬레이터 오류: ${error.message}`;
        }
    }
    updateSyntaxPreview();
}

addEvent("runButton", "click", runSimulation);

addEvent("saveButton", "click", function () {
    const inputEl = document.querySelector(".code-input");
    const filenameEl = byId("filenameInput");
    if (!inputEl || !filenameEl) return;
    const code = inputEl.value;
    let filename = filenameEl.value.trim();
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

addEvent("stopButton", "click", function () {
    const output = byId("runOutput");
    if (output) output.textContent = "시뮬레이션이 중지되었습니다.";
});

addEvent("githubButton", "click", function () {
    window.open("https://github.com/team-toyotech/", "_blank");
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
        const inputEl = document.querySelector(".code-input");
        if (inputEl) inputEl.value = tab.content;
        let fname = tab.filename.replace(/\.[^/.]+$/, "");
        if (!fname.endsWith(".koa")) fname += ".koa";
        const filenameEl = byId("filenameInput");
        if (filenameEl) filenameEl.value = fname;
        updateSyntaxPreview();
        updateLineNumbers();
    }
    renderTabs();
}

function renderTabs() {
    const tabBar = byId("tabBar");
    if (!tabBar) return;
    tabBar.innerHTML = "";
    const linenumDiv = byId("editorLinenum");
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
                                    t.id !== tab.id,
                            )
                        ) {
                            finalName = base + "_" + num;
                            num++;
                        }
                        tab.filename = finalName + ".koa";
                        const filenameEl = byId("filenameInput");
                        if (filenameEl) filenameEl.value = finalName + ".koa";
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

    if (editorRoot)
        editorRoot.style.setProperty("--editor-font-size", normalized);
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
        const response = await fetch(
            `examples/${encodeURIComponent(filename)}`,
        );
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
const filenameInput = byId("filenameInput");
if (codeInput) {
    codeInput.addEventListener("input", () => {
        const tab = tabs.find((t) => t.id === activeTabId);
        if (tab) tab.content = codeInput.value;
        updateLineNumbers();
        updateSyntaxPreview();
        isDirty = true;
    });
    codeInput.addEventListener("scroll", function () {
        const linenumLayer = byId("editorLinenum");
        const highlight = byId("codeHighlight");
        if (linenumLayer) linenumLayer.scrollTop = codeInput.scrollTop;
        if (highlight) {
            highlight.scrollTop = codeInput.scrollTop;
            highlight.scrollLeft = codeInput.scrollLeft;
        }
    });
    codeInput.addEventListener("click", updateLineNumbers);
    codeInput.addEventListener("keyup", updateLineNumbers);
    codeInput.addEventListener("select", updateLineNumbers);
}
if (filenameInput) {
    filenameInput.addEventListener("input", () => {
        const tab = tabs.find((t) => t.id === activeTabId);
        if (tab) {
            let fname = filenameInput.value.trim().replace(/\.[^/.]+$/, "");
            if (!fname.endsWith(".koa")) fname += ".koa";
            tab.filename = fname;
            filenameInput.value = fname;
        }
        renderTabs();
        isDirty = true;
    });
}

const openFileMenu = byId("openFileMenu");
addEvent(openFileMenu, "click", openFileHandler);

const openFileToolbarBtn = byId("openFileToolbarBtn");
addEvent(openFileToolbarBtn, "click", openFileHandler);

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

const newFileMenu = byId("newFileMenu");
addEvent(newFileMenu, "click", function () {
    let untitledNum = 1;
    let name;
    do {
        name = "untitled" + untitledNum + ".koa";
        untitledNum++;
    } while (tabs.some((t) => t.filename === name));
    createTab(name, "");
});

const tabAddBtn = byId("tabAddBtn");
addEvent(tabAddBtn, "click", function () {
    let untitledNum = 1;
    let name;
    do {
        name = "untitled" + untitledNum + ".koa";
        untitledNum++;
    } while (tabs.some((t) => t.filename === name));
    createTab(name, "");
});

addEvent("menuToggleBtn", "click", function () {
    const sideMenu = document.querySelector(".side-menu");
    const editorArea = document.querySelector(".editor-area");
    if (!sideMenu || !editorArea) return;
    sideMenu.classList.toggle("open");
    if (sideMenu.classList.contains("open")) {
        editorArea.style.marginLeft = "210px";
    } else {
        editorArea.style.marginLeft = "0";
    }
});

const settingsMenu = byId("settingsMenu");
const settingsModal = byId("settingsModal");
let settingsCloseBtn = byId("settingsCloseBtn");
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
if (settingsModal) settingsModal.appendChild(closeX);
closeX.addEventListener("click", function () {
    if (settingsModal) settingsModal.style.display = "none";
});

const bgRadios = document.getElementsByName("bgcolor");
const fontRadios = document.getElementsByName("fontsize");
const fontsizeInput = byId("fontsizeInput");
const fontsizeToggleBtn = byId("fontsizeToggleBtn");
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
        trimmedLine === "아니면" || /^아니면만약\s+.+\s+이면$/.test(trimmedLine)
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

const tabsizeInput = byId("tabsizeInput");
const tabsizeToggleBtn = byId("tabsizeToggleBtn");
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

addEvent(settingsMenu, "click", function () {
    if (!settingsModal || !fontsizeInput) return;
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
addEvent(settingsModal, "click", function (e) {
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

addEvent(fontsizeToggleBtn, "click", function () {
    if (!fontsizeInput) return;
    let size = parseInt(fontsizeInput.value, 10);
    if (isNaN(size) || size < 10) size = 10;
    if (size > 32) size = 32;
    fontsizeInput.value = size;
    applyEditorFontSize(size);
});
addEvent(fontsizeInput, "keydown", function (e) {
    if (e.key === "Enter" && fontsizeToggleBtn) fontsizeToggleBtn.click();
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

addEvent(codeInput, "keydown", function (e) {
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
    const input = byId("consoleInput");
    if (!pendingInputResolver || !input) return;
    const value = input.value;
    input.value = "";
    pendingInputResolver(value);
}

addEvent("consoleSubmit", "click", submitConsoleInput);
addEvent("consoleInput", "keydown", function (e) {
    if (e.key === "Enter" && (e.ctrlKey || e.metaKey)) {
        e.preventDefault();
        submitConsoleInput();
    }
});

addEvent("settingsToolbarBtn", "click", function () {
    const modal = byId("settingsModal");
    if (modal) modal.style.display = "flex";
});

addEvent("runMenu", "click", runSimulation);
addEvent("stopMenu", "click", function () {
    byId("stopButton")?.click();
});
addEvent("saveMenu", "click", function () {
    byId("saveButton")?.click();
});
addEvent("downloadMenu", "click", function () {
    openCompilerDownloadPage();
});
addEvent("examplesMenu", "click", openExamplePicker);
addEvent("exampleSelect", "change", function () {
    const filename = this.value;
    this.value = "";
    loadExample(filename);
});
addEvent("studyMenu", "click", function () {
    window.open("/docs/", "_blank");
});
