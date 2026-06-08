# 시작하기

## 설치하기

다운로드 페이지에서 내 운영체제에 맞는 설치 파일을 받습니다.

- Windows: `skkoa-windows.cmd`
- macOS: `skkoa-macos.command`
- Linux: `skkoa-linux.sh`

설치가 끝나면 새 터미널을 열고 아래 명령을 사용할 수 있습니다.

```bash
skkoa hello.koa
```

## 첫 파일 만들기

`hello.koa` 파일을 만들고 아래 내용을 넣습니다.

```koa
시작
    출력 "안녕하세요"
끝
```

실행합니다.

```bash
skkoa hello.koa
```

## 실행 파일 이름 정하기

```bash
skkoa hello.koa -o hello
```

Windows에서는 `hello.exe`, macOS와 Linux에서는 `hello` 실행 파일을 만들 수 있습니다.

## 모듈 가져오기

설치 파일에는 스택과 큐 모듈도 함께 들어 있습니다.

```koa
가져오기 "stack.koa"
가져오기 "queue.koa"
```

자세한 설치 구조나 직접 빌드 방법은 저장소의 `README.md`를 확인하세요.
