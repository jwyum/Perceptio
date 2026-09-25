# Percepio Tracealyzer for FreeRTOS (.NET 10 WPF)

**Percepio Tracealyzer for FreeRTOS**는 [Percepio Tracealyzer](https://percepio.com/tracealyzer/)의 핵심 기능을 데스크톱 환경에서 구현한 고성능 RTOS 시각화 & 타이밍 분석 도구입니다.

C# .NET 10 및 WPF 커스텀 Direct DrawingVisual 렌더링 엔진을 기반으로 개발되어 10만 개 이상의 RTOS 스케줄링 이벤트도 60 FPS로 매끄럽게 탐색(Zoom/Pan)할 수 있습니다.

---

## ✨ 핵심 기능 (Key Features)

### 1. 고성능 실행 타임라인 (Execution Timeline & Gantt Chart)
- **태스크 & ISR 전용 레인(Lane)**:
  - FreeRTOS 각 태스크 및 ISR별 스케줄링 구간(Execution Interval)을 색상 코드로 명확히 시각화
  - 선점(Preemption) 발생 시 적색 인디케이터 및 선점한 주체(Preempted By) 추적
  - 마이크로초(µs) ~ 초(s) 단위 적응형 가변 시간 눈금자(Time Ruler)
- **인터랙티브 탐색**:
  - **마우스 휠 줌(Zoom In/Out)**: 마우스 커서 위치를 중심으로 정밀 확대/축소
  - **드래그 팬(Pan)**: 좌클릭 드래그로 타임라인 좌우 스크롤
  - **전체 맞춤(Fit All)**: 원클릭으로 전체 트레이스 범위로 줌 복원
  - **미니맵(Minimap Overview)**: 전체 트레이스 조감도 및 뷰포트 탐색기 바

### 2. 정밀 델타 시간 측정 도구 (Delta Time & Frequency Measurement)
- **T1 / T2 듀얼 마커**:
  - 타임라인 좌클릭: **T1 마커 (시안색)** 설정
  - 타임라인 우클릭 또는 `Shift + 좌클릭`: **T2 마커 (노란색)** 설정
  - 두 마커 사이의 **경과 시간 ($\Delta T$)** 및 **주파수 ($1 / \Delta T$)** 실시간 자동 계산

### 3. 실시간 CPU 점유율 차트 (CPU Load Breakdown Chart)
- 뷰포트 시간 윈도우에 따른 태스크별 CPU 점유율 실시간 스택 차트 시각화
- 0~100% 점유율 추세 및 Idle 상태 대비 활성 태스크 부하 분석

### 4. 커널 동기화 & 이벤트 로그 뷰어 (Event Log & Sync History)
- 가상화 DataGrid로 수만 개의 이벤트를 지연 없이 스크롤
- **카테고리별 필터링**:
  - `Scheduling` (Task Switch In/Out)
  - `Interrupt (ISR)` (IsrEnter, IsrExit)
  - `Queue` (QueueSend, QueueReceive)
  - `Sync / Mutex` (MutexTake, MutexGive, Semaphore)
  - `Memory` (pvPortMalloc, vPortFree)
  - `User Event` (사용자 정의 로깅)
- **이벤트 클릭 동기화**: 테이블에서 이벤트를 클릭하면 타임라인이 해당 시점으로 자동 스크롤 및 중앙 정렬

### 5. 인스펙터 패널 (Inspector Panel)
- 선택된 실행 구간의 시작/종료 타임스탬프, 실행 지속 시간, 우선순위, 선점 여부 상세 확인

### 6. 다채로운 데이터 수집 모드 (Data Ingestion)
- **★ 원클릭 내장 FreeRTOS 시뮬레이터 (Demo Mode)**:
  - 하드웨어 연결 없이도 모터 제어, 센서 획득, 네트워크 통신, GUI 렌더러, 플래시 로깅, UART ISR 등 복합 FreeRTOS 환경의 동작을 즉시 체험
- **실시간 UART 시리얼 스트리밍 (Serial COM Port)**:
  - 115200 ~ 2000000 bps 고속 시리얼 실시간 수집 지원
- **TCP 소켓 / Segger RTT 연동**:
  - 타깃 보드 Wi-Fi/이더넷 또는 J-Link Segger RTT 서버를 통한 라이브 트레이스 스트리밍
- **파일 저장/불러오기 (Snapshot Mode)**:
  - JSON, CSV, FreeRTOS 바이너리 스냅샷 덤프 파일 지원

---

## 🚀 시작하기 (Getting Started)

### 요구 사양
- OS: Windows 10 / 11 (64-bit)
- 런타임: .NET 10 SDK 또는 .NET 10 Desktop Runtime

### 빌드 및 실행
```powershell
# 프로젝트 디렉터리 이동
cd c:\MyRepository\Peceptio

# 빌드
dotnet build

# 애플리케이션 실행
dotnet run
```

---

## 🛠 단축키 및 마우스 조작법

| 동작 | 조작 방법 |
| :--- | :--- |
| **타임라인 확대 / 축소** | 마우스 휠 위/아래 (커서 기준) 또는 상단 `➕ / ➖` 버튼 |
| **타임라인 좌우 스크롤(Pan)** | 빈 공간 좌클릭 드래그 또는 휠 버튼 드래그 |
| **마커 1 (T1) 배치** | 타임라인 상단 룰러 좌클릭 |
| **마커 2 (T2) 배치** | 타임라인 우클릭 또는 `Shift + 좌클릭` |
| **마커 초기화** | 상단 측정 패널의 `✕` 버튼 |
| **구간(Interval) 선택** | 타임라인 내 태스크 블록 클릭 |
| **이벤트 위치로 점프** | 하단 이벤트 로그 테이블에서 행 클릭 |
| **전체 뷰 맞춤** | 상단 `⇄ Fit` 버튼 클릭 |
| **라이브 팔로우 (Auto Scroll)** | 상단 `Auto Scroll` 토글 버튼 |

---

## 📡 FreeRTOS 펌웨어 연동 가이드

임베디드 보드(STM32, ESP32, NXP, RP2040 등)에서 PC로 트레이스 데이터를 전송하려면 `EmbeddedRecorder/` 폴더의 헤더와 소스를 참조하세요.

1. `FreeRTOSConfig.h`에 다음 매크로를 활성화:
```c
#define configUSE_TRACE_FACILITY 1
#include "trcRecorder.h"
```

2. 시리얼(UART) 또는 Segger RTT로 다음 CSV 포맷 라인을 전송:
```
[TIMESTAMP_US],[EVENT_TYPE],[ACTOR_ID],[ACTOR_NAME],[PRIORITY],[DETAILS]
```
예시:
```
12450.5,TaskSwitchIn,1,T_MotorCtrl,5,Motor PID Loop
12830.0,TaskSwitchOut,1,T_MotorCtrl,5,vTaskDelay(5)
12850.0,IsrEnter,11,SysTick_ISR,15,Kernel Tick
```

3. PC 프로그램에서 **UART COM** 포트와 보레이트(115200 등)를 선택하고 **Connect UART**를 누르면 실시간 타임라인이 그려집니다.

---

## 📂 프로젝트 아키텍처

```
Tracealyzer/
├── Controls/
│   ├── TraceTimelineControl.cs   # 커스텀 60FPS Direct Drawing 타임라인
│   ├── TraceMinimapControl.cs    # 조감도 미니맵 뷰포트 네비게이터
│   └── CpuLoadChartControl.cs    # 실시간 CPU 점유율 스택 차트
├── Models/
│   ├── ActorType.cs              # Task, ISR, Idle, Kernel 열거형
│   ├── TraceActor.cs             # 태스크/ISR 메타데이터 및 통계
│   ├── TraceEventType.cs         # 스케줄링/동기화/메모리 이벤트 종류
│   ├── TraceEvent.cs             # 타임스탬프 이벤트 모델
│   ├── ExecutionInterval.cs      # 실행 구간 및 선점(Preemption) 모델
│   ├── TraceMarker.cs            # T1, T2 마커 및 델타 시간 측정
│   └── TraceSession.cs           # 이진 탐색 및 스케줄링 상태 머신
├── Services/
│   ├── ITraceDataSource.cs       # 트레이스 소스 추상화 인터페이스
│   ├── TraceSimulationEngine.cs  # FreeRTOS 실시간/스냅샷 시뮬레이터
│   ├── SerialTraceReceiver.cs    # UART COM 포트 리시버
│   ├── TcpTraceReceiver.cs       # TCP 소켓 / Segger RTT 리시버
│   └── SnapshotFileParser.cs     # JSON/CSV/Binary 스냅샷 파서
├── ViewModels/
│   ├── ViewModelBase.cs          # INotifyPropertyChanged 기본 클래스
│   ├── RelayCommand.cs           # ICommand 구현체
│   └── MainViewModel.cs          # 전체 앱 상태, 뷰포트, 필터 관리
├── Themes/
│   └── DarkTheme.xaml            # 슬레이트/시안 프리미엄 다크 테마
├── SampleTraces/
│   └── freertos_sample.json      # 실무급 샘플 트레이스 데이터
└── EmbeddedRecorder/
    ├── trcRecorder.h             # FreeRTOS C 트레이스 훅 헤더
    └── trcRecorder.c             # FreeRTOS C 트레이스 훅 구현체
```
