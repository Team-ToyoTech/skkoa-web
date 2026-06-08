# 컴파일러

SKKOA 코드는 `skkoa` 명령으로 실행 파일을 만들 수 있습니다.

```bash
skkoa hello.koa
```

## 자주 쓰는 명령

```bash
skkoa hello.koa
skkoa hello.koa -o hello
skkoa hello.koa --emit-asm
```

- 첫 번째 명령은 기본 이름으로 실행 파일을 만듭니다.
- `-o`는 실행 파일 이름을 직접 정합니다.
- `--emit-asm`은 중간 결과를 보고 싶을 때 사용합니다.

## 웹 컴파일러

웹 페이지의 컴파일러는 문법을 빠르게 확인하고 예제를 시험해 보는 곳입니다.

실제 실행 파일을 만들려면 다운로드한 `skkoa` 명령을 사용하세요.

## 더 자세한 내용

컴파일러가 내부에서 어떤 단계를 거치는지, 어떤 도구를 사용하는지는 `README.md`와 `compiler/README.md`에 정리되어 있습니다.
