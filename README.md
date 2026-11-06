# 레식 서버 변경기 (Siege Server Changer)

Rainbow Six Siege의 매칭 서버(데이터센터)를 바꿔 주는 Windows 프로그램.

게임 설정 파일 `Documents\My Games\Rainbow Six - Siege\<프로필>\GameSettings.ini`의 `DataCenterHint` 한 줄만 바꾼다. 게임 파일, 메모리, 네트워크는 건드리지 않는다.

## 기능

- 프로필 자동 탐색 (OneDrive 문서 폴더 포함), 여러 프로필 일괄 적용
- 게임 설정 파일 주석에서 서버 목록을 읽음 (업데이트로 서버가 추가돼도 따라감)
- 리전별 핑 측정 (AWS 엔드포인트 TCP 연결 시간, 참고용) + 가장 빠른 서버 선택
- 즐겨찾기 ☆ / 최근 사용 서버
- 첫 수정 전 자동 백업 (`GameSettings.ini.bak`) 및 복원
- 게임 실행 중 경고, Steam / Ubisoft Connect로 게임 실행

## 빌드

필요: .NET 10 SDK, (설치 파일용) NSIS

```powershell
dotnet test SiegeServerChanger.slnx                                   # 테스트
powershell -ExecutionPolicy Bypass -File tools/publish.ps1            # publish/SiegeServerChanger.exe (단일 exe)
powershell -ExecutionPolicy Bypass -File tools/build-installer.ps1    # publish/SiegeServerChanger-Setup-<버전>.exe
```

설치 파일은 관리자 권한 없이 `%LOCALAPPDATA%\Programs\SiegeServerChanger`에 설치된다.

## 구조

- `src/SiegeServerChanger.Core` — 설정 파일 읽기/수정, 서버 목록, 핑, 프로그램 설정
- `src/SiegeServerChanger.App` — WPF UI
- `tests/SiegeServerChanger.Tests` — xUnit 테스트
- `installer/setup.nsi` — NSIS 설치 스크립트
- `PLAN.md` — 계획서
