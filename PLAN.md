# 레식 서버 변경기 (Siege Server Changer) — 계획서

작성일: 2026-11-06

## 1. 목표

Rainbow Six Siege의 매칭 서버(데이터센터)를 클릭 몇 번으로 바꿀 수 있는 Windows용 데스크톱 프로그램.

- 현재 설정된 서버를 보여 준다.
- 서버 목록에서 하나를 고르면 설정 파일에 바로 적용한다.
- (선택 기능) 각 서버의 핑을 재서 가장 가까운 서버를 추천한다.

## 2. 동작 원리

레식은 시작할 때 아래 파일의 `DataCenterHint` 값을 읽어서 어느 서버로 매칭할지 정한다. 서버 변경기는 **이 값 한 줄만 바꾼다**. 게임 실행 파일, 메모리, 네트워크 패킷은 건드리지 않으므로 안티치트(BattlEye)와 충돌하지 않는다. 이 파일을 수정하는 건 Ubisoft도 안내하는 방법이다.

```
%USERPROFILE%\Documents\My Games\Rainbow Six - Siege\<프로필 GUID>\GameSettings.ini
```

```ini
[ONLINE]
;DataCenterHint =>
;    default (ping based)
;    gamelift/ap-northeast-2
;    ...
DataCenterHint=gamelift/ap-northeast-2
```

이 PC에서 실제 파일을 확인한 결과:

- 서버 값은 이제 `gamelift/<AWS 리전>` 형식이다. 예전 형식인 `playfab/...`는 `playfab/uaenorth` 하나만 남아 있다.
- 쓸 수 있는 값의 목록이 파일 안에 **주석으로 들어 있다**. 그래서 목록을 코드에 박아 두지 않고 이 주석을 읽어서 만들면, 업데이트로 서버가 바뀌어도 그대로 따라갈 수 있다.

### 현재 서버 목록 (파일 주석 기준)

| 값 | 위치 |
|---|---|
| `default` | 핑 기준 자동 선택 |
| `gamelift/ap-northeast-2` | 서울 |
| `gamelift/ap-northeast-1` | 도쿄 |
| `gamelift/ap-northeast-3` | 오사카 |
| `gamelift/ap-east-1` | 홍콩 |
| `gamelift/ap-southeast-1` | 싱가포르 |
| `gamelift/ap-southeast-2` | 시드니 |
| `gamelift/ap-south-1` | 뭄바이 |
| `gamelift/eu-north-1` | 스톡홀름 |
| `gamelift/eu-west-1` | 아일랜드 |
| `gamelift/eu-west-2` | 런던 |
| `gamelift/eu-west-3` | 파리 |
| `gamelift/eu-central-1` | 프랑크푸르트 |
| `gamelift/eu-south-1` | 밀라노 |
| `gamelift/af-south-1` | 케이프타운 |
| `gamelift/sa-east-1` | 상파울루 |
| `gamelift/us-east-1` | 미국 버지니아 |
| `gamelift/us-east-2` | 미국 오하이오 |
| `gamelift/us-west-1` | 미국 캘리포니아 |
| `gamelift/us-west-2` | 미국 오리건 |
| `gamelift/ca-central-1` | 캐나다 중부 |
| `playfab/uaenorth` | UAE 북부 |

목록에 없는 값이 주석에 새로 생기면, 표시 이름 없이 리전 코드 그대로 보여 준다.

## 3. 기능 범위

### MVP (1차)
1. **프로필 찾기**: `내 문서` 경로는 `SHGetKnownFolderPath`로 얻는다. OneDrive로 옮겨진 문서 폴더도 이 방식으로 찾을 수 있다. 그 아래 `My Games\Rainbow Six - Siege\*`에서 `GameSettings.ini`가 있는 GUID 폴더를 모두 찾는다. `Benchmark` 같은 다른 폴더는 무시한다.
2. **여러 프로필**: 계정이 여러 개면 드롭다운으로 고르게 하고, "모든 프로필에 적용" 옵션도 둔다.
3. **현재 서버 표시**: 지금 설정된 `DataCenterHint` 값을 읽어서 보여 준다.
4. **서버 변경**: 목록에서 고르고 [적용]을 누르면 그 한 줄만 바꾼다. 줄 순서, 주석, 인코딩, 줄바꿈 형식(CRLF)은 그대로 둔다.
5. **백업**: 처음 수정하기 전에 `GameSettings.ini.bak`을 만들고, [백업에서 되돌리기] 버튼을 둔다.
6. **게임 실행 감지**: `RainbowSix.exe`나 `RainbowSix_BE.exe` 프로세스가 떠 있으면 경고한다. 게임은 종료할 때 설정을 다시 저장하기 때문에, 실행 중에 바꾼 값은 덮어써질 수 있다.

### 2차
7. **핑 측정** ✅ 구현됨: 리전마다 `dynamodb.<region>.amazonaws.com:443`에 TCP로 연결해 보고 걸린 시간을 잰다. `gamelift.<region>` 엔드포인트는 일부 리전(예: ap-east-1, eu-south-1)에 없어서 쓰지 않는다. `playfab/uaenorth`는 확인된 엔드포인트가 없어 "측정 불가"로 표시한다. ICMP 핑은 막혀 있는 경우가 많아서 TCP 방식을 쓴다. 3~5회 재서 중앙값을 낸다. 실제 게임 서버와의 핑과는 다를 수 있고, 리전끼리 비교하는 참고용이다.
8. **가장 빠른 서버 자동 선택** 버튼. ✅
9. **즐겨찾기 / 최근 사용 서버** ✅: ☆를 누르면 즐겨찾기가 되어 목록 위로 올라가고, 최근 적용한 서버 5개는 바로가기 버튼으로 표시된다. 저장 위치는 `%APPDATA%\SiegeServerChanger\settings.json`.
10. **게임 실행** 버튼 ✅: Steam은 `steam://rungameid/359550`, Ubisoft Connect는 `uplay://launch/635/0`으로 실행한다. 설치된 실행기는 레지스트리에서 찾고, 둘 다 있으면 고를 수 있다.

배포: `tools/publish.ps1`은 테스트를 통과해야 `publish/SiegeServerChanger.exe`(단일 exe, 약 62MB, .NET 설치 불필요)를 만든다. 아이콘은 `tools/make-icon.ps1`로 다시 만들 수 있다.

### 범위 밖
- 게임 프로세스나 메모리, 패킷을 건드리는 기능(밴 위험)
- 방화벽으로 특정 서버를 차단하는 방식(설정 파일로 충분하고, 시스템 설정을 바꿔야 함)

## 4. 기술 스택 (추천)

| 항목 | 선택 | 이유 |
|---|---|---|
| 언어/런타임 | C# / .NET 10 (LTS) | Windows 기본 지원, 단일 exe 배포 쉬움. .NET 8은 2026-11 지원 종료 |
| UI | WPF | 깔끔한 UI, 다크 테마 쉬움 |
| 배포 | `dotnet publish -r win-x64 --self-contained -p:PublishSingleFile=true` | .NET을 따로 설치할 필요 없음 |
| 권한 | 일반 사용자 | 내 문서 폴더만 수정하므로 관리자 권한 불필요 |

대안: Python + Tkinter(빠르게 만들 수 있지만 exe가 크고 백신이 오탐하기 쉬움), Tauri(예쁘지만 Rust와 웹 빌드 환경이 필요함).

## 5. 프로젝트 구조 (안)

```
Siege-Server/
├─ PLAN.md
├─ src/
│  ├─ SiegeServerChanger.Core/      # UI 없는 로직 (테스트 가능)
│  │  ├─ ProfileLocator.cs          # 프로필 폴더 탐색
│  │  ├─ GameSettingsFile.cs        # ini 읽기/한 줄 수정/백업
│  │  ├─ DataCenterCatalog.cs       # 주석 파싱 + 표시 이름 매핑
│  │  ├─ GameProcessDetector.cs     # 게임 실행 여부
│  │  └─ LatencyProbe.cs            # (2차) TCP 핑
│  └─ SiegeServerChanger.App/       # WPF UI
└─ tests/
   └─ SiegeServerChanger.Tests/     # xUnit: ini 파싱/수정 테스트
```

## 6. 화면 구성 (MVP)

```
┌───────────────────────────────────────────┐
│ 프로필: [xxxxxxxx-... ▼]  ☐ 모든 프로필    │
│ 현재 서버: 서울 (gamelift/ap-northeast-2) │
├───────────────────────────────────────────┤
│ ○ 자동 (핑 기준)                          │
│ ● 서울            ap-northeast-2   [28ms] │
│ ○ 도쿄            ap-northeast-1   [41ms] │
│ ○ 홍콩            ap-east-1        [65ms] │
│ ...                                       │
├───────────────────────────────────────────┤
│ [핑 측정] [적용] [백업 복원] [게임 실행]  │
│ ⚠ 게임이 실행 중입니다. 종료 후 적용하세요 │
└───────────────────────────────────────────┘
```

## 7. 개발 단계와 일정 (대략)

| 단계 | 내용 | 완료 기준 |
|---|---|---|
| 1 | Core: 프로필 탐색, ini 읽기/쓰기, 주석 파싱 + 단위 테스트 | 테스트 통과, 실제 파일 변경 후 diff가 한 줄만 나옴 |
| 2 | WPF UI MVP (기능 1~6) | 서버 변경 → 게임 안에서 서버 바뀐 것 확인 |
| 3 | 핑 측정 + 자동 선택 (7~8) | 리전별 ms 표시 |
| 4 | 즐겨찾기, 게임 실행, 아이콘, 단일 exe 빌드 (9~10) | 배포용 exe 하나 |

## 8. 위험 요소와 대응

| 위험 | 대응 |
|---|---|
| 게임이 실행 중이면 바꾼 설정을 덮어씀 | 실행 감지 후 경고 |
| 업데이트로 키 이름이나 형식이 바뀜 | 주석에서 목록을 읽음, 키를 못 찾으면 수정하지 않고 오류 표시 |
| 파일 인코딩이나 줄바꿈이 깨짐 | 원본 인코딩과 줄바꿈을 감지해서 그대로 유지, 수정 전 백업 |
| 핑 수치가 실제 게임 핑과 다름 | UI에 "참고용" 표시 |
| 파일이 읽기 전용이거나 OneDrive가 동기화 중임 | 쓰기 실패 시 원인 안내, 재시도 |

## 9. 정해야 할 것

- [ ] UI 기술: WPF(추천) / WinForms / Python
- [ ] MVP에 핑 측정까지 넣을지
- [ ] UI 언어: 한국어만 / 한국어+영어
