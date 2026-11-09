# DepalAdmin — 관리자 프로그램

AA 건전지 분류 라인의 관리자 화면 (WPF, .NET 8).
역할분담표(`관리자프로그램_역할분담 완료.xlsx`)대로 **한 프로젝트 = 한 담당자**로 나눠 둔 틀이다.
지금 상태로도 F5를 누르면 가짜 데이터로 전부 동작한다. 각자 자기 프로젝트에서 출발해 키우면 된다.

---

## 1. 처음 실행

1. Visual Studio 2022 (".NET 데스크톱 개발" 워크로드) 설치
2. `DepalAdmin.sln` 열기 → 시작 프로젝트가 `DepalAdmin.Shell`인지 확인
   (솔루션 탐색기에 사람 이름 폴더가 있고, 그 안에 자기 프로젝트가 있다)
3. **F5** → 로그인 창
   - 처음 계정: `admin` / `admin1234` → 들어가자마자 새 비밀번호를 정하게 된다
   - 계정·작업 기록·인수인계·채팅 기록은 실행 파일 옆 `data/` 폴더에 생긴다 (git 제외)
4. 라인 현황에서 [시작]을 누르면 1초마다 공정이 진행되고 결과·이벤트·알람이 쌓인다.
   채팅에는 45초마다 '현장 작업자(가짜)'가 말을 건다.

실제 서버에 붙일 때는 `src/DepalAdmin.Shell/settings.json` (또는 **설정** 탭)에서
`UseFakeMqtt: false`, `BrokerHost: 서버PC_IP`. MySQL은 `UseFakeDb: false` + 접속 문자열.
DB 비밀번호는 `settings.json`이 아니라 같은 폴더의 `settings.local.json`에 둔다 (git 제외).

---

## 2. 담당표 ★

| 담당 | 프로젝트 | 탭·기능 |
|---|---|---|
| **조시현** (부팀장, 공통 틀) | `Shell` `Core` `Comm` | 메인 창·탭·잠금·비상정지, 서버 통신(MQTT), 데이터 모양, 공통 스타일, 합치기 |
| | `Feature.Devices` | 장치 상태 — 장치 상태 카드 |
| | `Feature.Stats` | 통계 |
| | `Feature.Report` | 보고서 |
| | `Feature.Chat` | 채팅 |
| **장성찬** | `Feature.Auth` | 로그인 · 계정 · 작업 기록 |
| | `Feature.EventLog` | 이벤트 로그 |
| | `Feature.History` | 이력 조회 (MySQL 읽기, 통계·보고서도 이걸 씀) |
| **유동호** | `Feature.Operation` | 라인 현황의 조작 버튼 패널 |
| | `Feature.Handover` | 인수인계 |
| **노원종** (하드웨어) | `Feature.Settings` | 설정 |
| | `Feature.Alarms` | 알람 탭 · 맨 아래 알람 줄 |
| | `Feature.PiStatus` | 장치 상태의 라즈베리파이 세부 패널 |
| **김하균** (하드웨어) | `Feature.Results` | 검사 결과 표·상세 · 라인 현황의 팔레트 2×5 맵 패널 |
| **김초아** (팀장, 하드웨어) | `Feature.Process` | 라인 현황 — 공정 흐름도 · 분류 개수·현재 상태 · AGV |

각 `.csproj` 맨 위와 `XxxModule.cs`에도 담당자 이름이 적혀 있다. 코드 안의 `TODO(이름)`이 그 사람이 먼저 할 일.

### 탭 순서

| F키 | 탭 | 담당 | 권한 |
|---|---|---|---|
| F1 | 라인 현황 | 김초아 (+ 팔레트 김하균, 버튼 유동호) | 모두 |
| F2 | 알람 | 노원종 | 모두 |
| F3 | 장치 상태 | 조시현 (+ 라파 세부 노원종) | 모두 |
| F4 | 검사 결과 | 김하균 | 모두 |
| F5 | 이벤트 로그 | 장성찬 | 모두 |
| F6 | 채팅 | 조시현 | 모두 |
| F7 | 인수인계 | 유동호 | 모두 (쓰기는 작업자 이상) |
| F8 | 이력 조회 | 장성찬 | 모두 |
| F9 | 통계 | 조시현 | 모두 |
| F10 | 보고서 | 조시현 | 모두 (인쇄는 작업자 이상) |
| F11 | 계정 | 장성찬 | 관리자 |
| F12 | 작업 기록 | 장성찬 | 관리자 |
| - | 설정 | 노원종 | 관리자 |

보기 전용·작업자 계정은 관리자 탭이 안 보여서 F키 번호가 앞으로 당겨진다. 순서를 바꾸려면 페이지의 `Order` 숫자만 고친다.

### 남의 화면에 끼우는 패널

한 화면에 두 사람 것이 같이 보이는 곳은 **패널**로 나눴다. 자리 주인은 `ContentControl` 하나만 두고, 패널 주인이 내용을 만든다.
서로 파일을 건드리지 않으니 Git 충돌이 없다.

| 자리 (`PanelSlots`) | 화면 / 자리 주인 | 패널 주인 |
|---|---|---|
| `LineControls` | 라인 현황 오른쪽 / 김초아 | 유동호 (조작 버튼) |
| `LinePallet` | 라인 현황 가운데 / 김초아 | 김하균 (팔레트 맵) |
| `DevicesPi` | 장치 상태 오른쪽 / 조시현 | 노원종 (라파 세부) |
| `ShellAlarmBar` | 메인 창 맨 아래 / 조시현 | 노원종 (알람 줄) |

패널의 바깥 상자(제목 줄)는 자리 주인이 그리고, 패널은 안쪽 내용만 그린다.
새 자리가 필요하면 조시현에게 요청.

### 기능끼리 주고받는 것

| 쓰는 쪽 | 받아 쓰는 것 | 주인 | 바꿀 때 |
|---|---|---|---|
| 통계·보고서 (조시현) | `IHistoryRepository` (결과·이벤트 수) | 장성찬 (MySQL 구현) | 메서드 모양을 바꾸면 조시현에게 알림 |
| 보고서 (조시현) | `IHandoverStore` (인수인계 메모) | 유동호 | `HandoverNote` 칸을 바꾸면 조시현에게 알림 |
| 인수인계 (유동호) · 채팅 (조시현) | `IAlarmService` (미확인 알람 수, 알람 목록) | 노원종 | |
| 모두 | `ILineClient`, `Session`, `Navigator` | 조시현 | |

---

## 3. 들어 있는 기능

| 탭 | 기능 |
|---|---|
| 라인 현황 | 공정 흐름도(지금 공정 표시), 팔레트 2×5 칸별 결과, 등급별 개수·A 비율, 현재 상태, AGV 위치·적재·기록, 시작·일시정지·재개·회차 종료·건너뛰기·비상정지 해제 (위험한 명령은 확인 창) |
| 알람 | WARN·ERROR 이벤트와 서버 연결 끊김을 알람으로. 반복 묶기, 확인(누가·언제), 모두 확인, 고장 알람 소리, CSV, 맨 아래 알람 줄, 탭 배지 |
| 장치 상태 | 장치별 하트비트 카드(정상·지연·끊김, 바뀐 시각, 이상일 때만 색), 라파 세부(집게·팔·아두이노 + 할 일 안내) |
| 검사 결과 | 실시간 결과 표(외관·극 방향·전압·내부저항·배출 위치), 등급 거르기, 받은 JSON 원문 |
| 이벤트 로그 | 실시간 로그, 등급·장치 거르기, 글자 찾기, CSV |
| 채팅 | 같은 서버에 붙은 관리자 프로그램끼리 메시지, 알람·건전지 결과 붙여 보내기, 탭 배지(안 읽은 수), 지난 7일 대화 불러오기 |
| 인수인계 | 메모 쓰기(라인 상태·회차·미확인 알람 수 자동 첨부), 기간·글자 찾기, 읽음 확인, 로그인 때 안 읽은 메모가 있으면 이 탭으로 이동 + 안내, 탭 배지 |
| 이력 조회 | 기간·등급·회차로 조회, 건전지별 / 회차별 보기, CSV |
| 통계 | 등급 비율, 시간대별 처리량, 불량·재분류 사유 순위, 무부하 전압 분포(임시 기준선), 1개당 평균 처리 시간 |
| 보고서 | 일일 / 회차 보고서(처리 개수·등급 비율·사유·이벤트 수·회차 표·인수인계 메모), A4 미리보기, 인쇄 (PDF는 "Microsoft Print to PDF") |
| 계정 | 계정 추가(임시 비밀번호 발급), 권한 변경, 비밀번호 초기화, 사용 중지, 삭제 |
| 작업 기록 | 로그인·로그아웃·라인 명령·비상정지·알람 확인·인수인계·보고서 인쇄·계정·설정 변경 기록, CSV |
| 설정 | 연결·DB·자동 로그아웃·알람 소리·라인 이름 (settings.local.json에 저장) |

**화면 공통**
- 로그인 필수. 5번 틀리면 30초 잠김, 임시 비밀번호는 첫 로그인 때 바꾸게 함, 비밀번호는 PBKDF2 해시로만 저장
- 권한 3단계 — 보기 전용 / 작업자 / 관리자. 권한이 없는 탭은 안 보이고 버튼은 꺼짐
- 자동 로그아웃 (기본 10분, 1분 남으면 위쪽 이름 밑에 표시) → 잠금 화면
- **비상정지는 로그인·권한·잠금과 관계없이 항상 누를 수 있음**, 확인 창 없이 바로 전송
- 탭 배지: 페이지 ViewModel이 `IHasBadge`를 구현하면 탭 이름 옆에 숫자가 뜬다 (알람·장치 상태·채팅·인수인계)

**화면 규칙 (ISA-101 고성능 HMI)** — 실제 공장 감시 화면에서 쓰는 방식
- 바탕은 회색, 정상은 색을 쓰지 않는다. 색은 이상일 때만 (주의 = 호박색, 고장 = 빨강)
- 묶음은 제목 줄이 붙은 사각 상자 (`Section` 스타일), 숫자는 Bahnschrift(표지판 글꼴)
- 새 화면도 이 규칙을 따른다. 색·글꼴은 `Core/Themes/Common.xaml`의 키만 쓴다

---

## 4. 합치기 규칙 ★

1. **자기 프로젝트 폴더 안만 고친다.** (2장 담당표)
2. **`Shell` `Core` `Comm` `Directory.*.props`는 조시현만 고친다.** 필요하면 요청 → 반영 후 공지.
   (예: 새 데이터 칸, 새 공통 스타일, 새 NuGet 패키지, 새 패널 자리)
3. **서버 데이터는 `ILineClient`로만** 받는다. 페이지에서 MQTTnet을 직접 쓰지 않는다. 토픽은 `Topics`, 명령은 `UiActions`.
4. **색·글꼴은 `Common.xaml` 키**, 한글 표시는 `Labels` (`Labels.Grade("NG")` → "불량").
5. **등급 기준을 화면에서 계산하지 않는다.** 서버가 준 `grade` 그대로. 기준값은 아직 임시.
6. **권한은 `Session`으로 확인** (`session.Can(Permission.OperateLine)`). 명령 전송은 공통 틀이 한 번 더 확인하고 작업 기록에 남긴다.
7. **다른 탭으로 보내려면 `Navigator`** (`navigator.Navigate("알람")`). 남의 ViewModel을 직접 부르지 않는다.
8. **패키지 버전은 `Directory.Packages.props` 한곳에서만.**
9. 서버에서 받은 이벤트는 **이미 UI 스레드**에서 불린다. `Dispatcher.Invoke` 필요 없음.

### Git

```
main            ← 항상 실행되는 상태. 직접 push 금지
 └ cho-sihyun, jang-seongchan, yu-dongho, noh-wonjong, kim-hagyun, kim-choa   (각자 브랜치)
```

- 하루 한 번 이상 `main`을 받아와 자기 브랜치에 합치기
- 다 되면 Pull Request → 조시현이 실행 확인 후 합침
- 빌드 안 되는 코드는 올리지 않기

---

## 5. 페이지에서 쓸 수 있는 것

ViewModel 생성자에 필요한 것을 적으면 자동으로 넣어 준다 (DI).

```csharp
public MyViewModel(ILineClient client, Session session, IAlarmService alarms, Navigator navigator,
                   IHistoryRepository repo, IHandoverStore handover, IAuditLog audit, AppSettings settings) { ... }
```

| 이름 | 내용 |
|---|---|
| `client.LineStatusReceived` | 라인 상태 — mode, step, counts, agv |
| `client.DevicesReceived` | 장치 상태 전체 |
| `client.EventReceived` / `ResultReceived` | 이벤트 1건 / 건전지 결과 1건 |
| `client.LastLine`, `client.LastDevices` | 마지막으로 받은 값 |
| `client.SendCommandAsync(UiActions.Start)` | 버튼 명령 (권한 없으면 예외) |
| `session.User`, `session.Can(...)`, `session.Changed` | 로그인한 사람, 권한 |
| `alarms.Alarms`, `alarms.UnackedCount`, `alarms.Raise(...)` | 알람 |
| `navigator.Navigate("탭 이름")`, `navigator.CurrentTitle` | 탭 이동, 지금 보는 탭 |
| `repo.GetResultsAsync(...)`, `repo.GetEventCountsAsync(...)` | 지난 결과, 이벤트 수 |
| `handover.GetAsync(...)` | 인수인계 메모 |
| `audit.Write("종류", "내용")` | 작업 기록 남기기 |
| `CsvExport.Save(...)` | 표를 CSV로 (엑셀 한글 OK) |

**새 탭 추가**: 자기 프로젝트의 `XxxModule.cs`에 `PageBase` 클래스 하나 + 등록 두 줄. 메인 창은 안 고쳐도 된다.
**남의 화면에 끼우기**: `PanelBase` 클래스 + `services.AddSingleton<IPanel, …>()`. 자리 이름은 `PanelSlots`.
**탭 배지**: ViewModel에 `IHasBadge`를 붙이고 `[ObservableProperty] private int _badgeCount;`

---

## 6. 확인 필요

| 항목 | 누구와 | 파일 |
|---|---|---|
| 채팅 상대 (관리자 프로그램 여러 대 / 작업자 메인 프로그램 포함?) | 팀 | `Feature.Chat/ChatViewModel.cs` |
| 채팅 토픽 `chat/messages`를 통신 규약에 추가 | 조시현 | `Core/Messaging/IChatClient.cs` |
| 채팅·인수인계·계정을 파일에 둘지 MySQL에 둘지 (지금 DB에는 이 테이블이 없음 → 파일 저장) | 각 담당 + DB 담당 | `ChatLogFile.cs`, `JsonHandoverStore.cs`, `JsonUserStore.cs` |
| 보고서에 넣을 항목 | 팀 | `Feature.Report/ReportData.cs` |
| `server/devices` 실제 형식, 상태 값 | 조시현 (central_server.py) | `Comm/DeviceParser.cs`, `Core/Labels.cs` |
| 라파 하트비트의 gripper·arm·arduino 실제 값 | 노원종 | `Feature.PiStatus/PiStatusViewModel.cs` |
| 화면용 MySQL 읽기 전용 계정 (SQL은 teamproject.sql 2026-11-06 기준으로 맞춤) | 장성찬 + DB 담당 | `Feature.History/MySqlHistoryRepository.cs` |
| 버튼 활성 조건, 수동 모드 | 유동호 + 팀 | `Feature.Operation/Panel/ControlPanelViewModel.cs` |
| 공정 순서·이름 | 김초아 | `Feature.Process/ProcessViewModel.cs` |
| 팔레트 칸 번호 방향 | 김하균 | `Feature.Results/Pallet/PalletViewModel.cs` |
| AGV_BATCH 확정 (잠정 8) | 김초아 + 팀 | 통신 규약 3-3 |
| 통계 기준선 1.30 / 1.50 V | 팀 (전압 시험 후) | `Feature.Stats/StatsViewModel.cs` |
