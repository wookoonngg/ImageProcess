# WEEK 1 발표자료 정리

> 목적: PPT에 바로 옮길 수 있도록 **요구사항 → 화면 → 사용자 액션 흐름 → 기능 정의 → 프로젝트 구조** 순으로 정리한다.  
> 발표 한 줄: *“BMP를 열어 원본/결과를 비교하고, ROI·Histogram·영상처리 화면까지 갖춘 검사 SW Viewer를 만들었다.”*

---

## 0. 발표 슬라이드 목차 (권장)

1. 프로젝트 배경 / 목적  
2. 요구사항 정의 (필수 / WEEK 1 / 향후)  
3. 화면 정의 (레이아웃)  
4. 기능 정의  
5. 사용자 액션별 내부 흐름  
6. 프로젝트 구조 / 클래스 설계 의도  
7. Demo  
8. 이슈와 해결 (5.6GB BMP, Dolphin UI)  
9. 다음 주 계획  

클래스 다이어그램 상세는 `Docs/ClassDiagram.md` 와 함께 사용한다.

---

## 1. 요구사항 정의

### 1.1 배경

| 항목 | 내용 |
|------|------|
| 과제 | WPF 기반 영상처리 프로그램 4주 개발 |
| 목적 | 반도체 검사 장비 SW에 필요한 GUI / OOP / 영상처리 / 구조 설계 역량 증명 |
| 제약 | OpenCV, CUDA, IPP 사용 금지 / 영상처리 클래스는 C++ / Dolphin UI 적용 |

### 1.2 실제 장비 SW와의 대응

```text
실제 장비                         본 프로젝트 (WEEK 1)
Camera  → Camera SDK              BMP 파일  → ImageFileService
Image Acquisition                 ReadHeader + Load / Preview
Image Processing                  WEEK 2~3 C++ DLL (예정)
Inspection Result                 Image Viewer 2
HMI / 결과 표시                   MainWindow + Dolphin UI
```

### 1.3 필수 요구사항 (과제 전체)

| ID | 요구사항 | WEEK | 현재 |
|----|----------|------|------|
| FR-01 | Image Viewer 1 (원본) | 1 | 구현 |
| FR-02 | Image Viewer 2 (처리 결과) | 1 | 화면 구현, 결과는 WEEK 2부터 연결 |
| FR-03 | Navigator (전체 Preview + 현재 뷰포트) | 1 | 구현 |
| FR-04 | BMP Open / Save | 1 | 구현 (대용량은 Preview) |
| FR-05 | ROI Select / Cancel / Template 등록 | 1 화면, 2 완성 | UI + ROI 드래그 구현, Template는 스텁 |
| FR-06 | Processing Time 표시 | 1 UI, 2 연동 | 상태바 UI 구현 |
| FR-07 | Histogram Viewer (ROI 기준) | 2 | 화면 구현, 실제 계산은 WEEK 2 |
| FR-08 | Dilation / Erosion / Smoothing / Threshold | 2 | 버튼 UI만 |
| FR-09 | Gaussian / Laplacian / Sobel | 3 | 버튼 UI만 |
| FR-10 | Template Matching DIFF / CORR / COEFF | 3 | 버튼 UI만 |
| NFR-01 | Dolphin UI (AtiWpf.Ui) 적용 | 1 | 적용 |
| NFR-02 | UI와 영상처리 로직 분리 | 1~4 | Service / Model / Control 분리 |
| NFR-03 | 예외처리 (파일 없음, 잘못된 BMP, 메모리) | 1 | 기본 적용 |
| NFR-04 | 영상처리 외부 라이브러리 금지 | 전 기간 | OpenCV 등 미사용 |

### 1.4 WEEK 1 범위 (이번 발표에서 말할 것)

**이번 주 목표:** BMP를 불러와 화면에 표시하고, 이후 기능을 붙일 수 있는 **기반 Viewer** 를 만든다.

포함:

- 프로젝트 구조 설계
- Dolphin Dark Theme UI
- BMP Open / Save
- Viewer 1, Viewer 2, Navigator
- 확대/축소, 이동
- ROI Select / Cancel 화면 동작
- 대용량 BMP Preview 모드

제외 (버튼만 배치):

- 실제 팽창/수축/필터/매칭 연산 (C++ DLL)

---

## 2. 화면 정의

### 2.1 메인 화면 레이아웃

```text
┌─────────────────────────────────────────────────────────────────┐
│ 메뉴: 파일(열기 / 저장 / 종료)                                    │
├──────────┬──────────────────┬──────────────────┬────────────────┤
│ 도구 패널 │ Image Viewer 1   │ Image Viewer 2   │ Navigator      │
│          │ (원본)            │ (처리 결과)      │                │
│ BMP Open │                  │                  ├────────────────┤
│ BMP Save │  휠: 확대/축소    │  처리 결과 표시   │ Histogram      │
│ ROI      │  드래그: 이동     │                  │                │
│ 영상처리 │  ROI Select 시    │                  ├────────────────┤
│ Filter   │  드래그 = ROI     │                  │ Matching 결과  │
│ Matching │                  │                  │ Score/Position │
├──────────┴──────────────────┴──────────────────┴────────────────┤
│ Status │ 이미지 정보(크기/bit/파일명) │ Processing Time: xx ms    │
└─────────────────────────────────────────────────────────────────┘
```

### 2.2 화면 영역 역할

| 영역 | 화면 이름 | 역할 |
|------|-----------|------|
| 좌측 | 도구 패널 | 사용자 명령 진입점. 기능은 버튼으로만 노출 |
| 중앙 좌 | Viewer 1 | 원본 이미지. ROI는 여기서만 선택 |
| 중앙 우 | Viewer 2 | 처리 결과. WEEK 1은 원본 복사/Preview |
| 우측 상 | Navigator | 전체 이미지 Preview + 현재 보이는 영역 |
| 우측 중 | Histogram | ROI 밝기 분포 (WEEK 2 실제 계산) |
| 우측 하 | Matching 결과 | Score / Position / ROI 정보 |
| 하단 | StatusBar | 상태, 파일 정보, 처리 시간 |

### 2.3 왜 Viewer를 둘로 나눴는가 (발표 포인트)

검사 장비는 **원본(카메라)** 과 **처리 결과(알고리즘)** 를 동시에 비교한다.  
한 Viewer에 덮어쓰면 “무엇이 바뀌었는지” 설명이 불가능하다.

---

## 3. 기능 정의

### 3.1 WEEK 1 구현 기능

| 기능 | 사용자에게 보이는 것 | 내부 책임 클래스 |
|------|----------------------|-----------------|
| BMP Open | 파일 대화상자 → Viewer/Navigator 표시 | `MainWindow` → `ImageFileService` → `ImageConverter` |
| BMP Save | 결과 이미지를 BMP로 저장 | `ImageFileService.SaveImage` |
| Header 읽기 | 상태바에 Width×Height, bit, 파일크기 | `ReadHeader` → `BitmapFileInfo` |
| Preview 모드 | 512MB 초과 파일은 다운샘플 표시 | `ImageConverter.LoadPreviewFromFile` |
| Zoom | 휠로 확대/축소 | `ImageViewerControl` |
| Pan | 드래그로 이동 | `ImageViewerControl` |
| Navigator 연동 | 확대/이동 시 하늘색 사각형 이동 | `ViewportChanged` → `NavigatorControl` |
| ROI Select | 토글 ON 후 드래그, 노란 사각형 | `ImageViewerControl` → `RoiData` |
| ROI Cancel | ROI 제거, Histogram 초기화 | `MainWindow.RoiCancel` |
| Template 등록 | ROI 없으면 경고, 있으면 등록 메시지 | WEEK 1 스텁 |
| Processing Time | 버튼 클릭 시 ms 표시 | `Stopwatch` (WEEK 1은 UI 경로 시간) |
| Dolphin UI | Dark Theme, ButtonMedium 등 | `AtiWpf.Ui` + `DolphinTheme.xaml` |

### 3.2 WEEK 2~3 버튼 (화면만, 연산 예정)

| 그룹 | 버튼 | 이후 연결 |
|------|------|-----------|
| 영상처리 | Dilation, Erosion, Smoothing, Threshold | C++ Morphology / Threshold |
| Filter | Gaussian, Laplacian, Sobel | C++ Filter |
| Matching | DIFF, CORR, COEFF | C++ Template Matching |

발표 시 표현: *“화면과 명령 경로는 WEEK 1에 고정했고, 알고리즘만 C++로 교체한다.”*

---

## 4. 사용자 액션별 내부 흐름

### 4.1 BMP Open

```text
[사용자] 파일 → 열기  또는  BMP Open 버튼
    ↓
[화면] OpenFileDialog (*.bmp)
    ↓
[MainWindow.LoadImageFromPath]
    ↓
[ImageFileService.ReadHeader]
    - 'BM' 시그니처 확인
    - Width / Height / BitDepth / Compression
    - BitmapFileInfo.IsValid()
    ↓
파일 크기 > 512MB ?
    YES → Preview 모드
          ImageConverter.LoadPreviewFromFile (DecodePixelWidth=4096)
    NO  → ImageFileService.LoadImage → Bitmap
          ImageConverter.BitmapToBitmapImage
    ↓
[화면 갱신]
    Viewer1.SetImage
    Viewer2.SetImage
    Navigator.SetPreviewImage
    Histogram.Clear
    StatusBar: 크기 / bit / 파일명 / Preview 여부
```

**화면 결과:** Viewer 1·2에 이미지, Navigator에 축소본, 상태바에 메타데이터.

**실패 시:** MessageBox + StatusBar에 오류 메시지  
(파일 없음, BMP 아님, 지원하지 않는 bit, 압축 BMP)

### 4.2 확대 / 이동 / Navigator

```text
[사용자] Viewer 1에서 휠
    ↓ ImageViewerControl.ApplyZoom
    ↓ ViewportChanged 이벤트
    ↓ NavigatorControl.UpdateViewport
    ↓ [화면] Navigator 위 하늘색 사각형 = 현재 보이는 영역

[사용자] Viewer 1에서 드래그 (ROI 모드 OFF)
    ↓ Pan (ScrollViewer offset 변경)
    ↓ 동일하게 Navigator 사각형 이동
```

**개념:** 화면 좌표(ScrollViewer) ≠ 이미지 좌표(픽셀). WEEK 2 ROI에서 이 차이를 설명한다.

### 4.3 ROI Select

```text
[사용자] ROI Select 토글 ON
    ↓ SourceViewer.RoiSelectMode = true (커서: Cross)
    ↓
[사용자] Viewer 1에서 드래그
    ↓ 화면: 노란색 ROI 사각형
    ↓ 마우스 업 시 RoiData(StartX, StartY, Width, Height)
    ↓ RoiSelected 이벤트
    ↓ MainWindow.OnRoiSelected
    ↓ [화면] Histogram 갱신, ROI 정보 텍스트
```

**WEEK 1 한계:** Histogram은 ROI 크기 기반 placeholder. 실제 픽셀 히스토그램은 WEEK 2.

### 4.4 ROI Cancel

```text
[사용자] ROI Cancel
    ↓ Viewer ROI 사각형 제거
    ↓ _currentRoi = null
    ↓ Histogram.Clear
    ↓ 토글 OFF, Pan 모드 복귀
```

### 4.5 BMP Save

```text
[사용자] BMP Save
    ↓ 결과 이미지 없으면 안내
    ↓ SaveFileDialog
    ↓ Preview 모드: 표시용 이미지를 BMP로 저장 (전체 해상도 아님)
    ↓ 일반 모드: _resultImage Bitmap 저장
    ↓ StatusBar: 저장 완료
```

### 4.6 영상처리 / Filter / Matching 버튼 (WEEK 1)

```text
[사용자] Gaussian 등 클릭
    ↓ 이미지 미개방이면 경고
    ↓ Stopwatch 시작
    ↓ StatusBar: "C++ DLL 연동 예정"
    ↓ Processing Time: 경과 ms 표시
```

**발표 포인트:** 버튼 → MainWindow → (향후) Processor 인터페이스.  
UI를 다시 만들지 않고 C++만 붙이면 된다.

---

## 5. 프로젝트 구조

### 5.1 폴더 구조

```text
ImageProgram/
├── App.xaml / App.xaml.cs          진입점, Dolphin Theme 등록
├── MainWindow.xaml(.cs)            화면 오케스트레이션
├── Controls/
│   ├── ImageViewerControl          Zoom / Pan / ROI
│   ├── NavigatorControl            Preview + Viewport
│   └── HistogramControl            Histogram 그리기
├── Models/
│   ├── ImageData                   메모리 이미지 + Dispose
│   ├── BitmapFileInfo              BMP 헤더 DTO
│   └── RoiData                     ROI 좌표
├── Services/
│   ├── IImageFileService           I/O 계약
│   ├── ImageFileService            BMP Read/Load/Save
│   ├── ILogger / ConsoleLogger
├── Utils/
│   ├── ImageConverter              Bitmap ↔ BitmapImage
│   ├── ValidationHelper
│   └── Constants
└── Resource/Styles/DolphinTheme.xaml
```

### 5.2 계층과 의존 방향

```text
MainWindow / Controls     ← UI는 여기만
        ↓
   Services / Models      ← 업무 규칙, 데이터
        ↓
      Utils               ← 변환, 검증

Processing(C++)  ← WEEK 2부터 MainWindow가 호출만 함 (단방향)
```

하위 계층이 Window를 몰라야 한다. (관심사 분리)

### 5.3 주요 클래스 책임 (한 줄)

| 클래스 | 한 가지 책임 |
|--------|----------------|
| `MainWindow` | 화면 연결, 사용자 명령 분배 |
| `ImageViewerControl` | 이미지 보여주기 + 좌표 입력 |
| `NavigatorControl` | 전체 대비 현재 위치 |
| `ImageFileService` | BMP 파일만 다룬다 |
| `BitmapFileInfo` | 헤더 값. I/O 로직 없음 |
| `ImageData` | 픽셀 메모리와 수명(Dispose) |
| `RoiData` | 관심 영역 좌표 값 |

### 5.4 Dolphin UI 적용 방식

```text
App.xaml
  → DolphinTheme.xaml
      → AtiWpf.Ui ColorsDark / Button / GroupBox / ...
MainWindow
  → Style="{StaticResource ButtonMedium}" 등 명시 적용
```

AtiWpf.Ui는 Button 암시적 Style이 없어, **반드시 named Style을 지정**해야 한다.

---

## 6. Demo 시나리오 (금요일)

```text
1. 프로그램 실행 → Dolphin Dark UI 확인
2. BMP Open → 작은 BMP 또는 Foundry Preview
3. 상태바에서 Width / Height / BitDepth 설명
4. Viewer 1 휠 확대 → Navigator 사각형 이동
5. ROI Select → 드래그 → Histogram / ROI 텍스트
6. ROI Cancel
7. BMP Save
8. Filter 버튼 클릭 → Processing Time UI만 시연
   “연산은 WEEK 2~3 C++”
```

대용량 `Foundry_4umDF 1.bmp` (약 5.6GB, 75348×75194, 8bit):  
**헤더는 읽고, 화면은 Preview.** 전체 로드는 OOM 가능 → 이 결정을 이슈로 발표한다.

---

## 7. 슬라이드에 넣을 문장 예시

- **목표:** 검사 SW처럼 원본과 결과를 나누어 보는 Viewer 기반을 만들었다.  
- **구조:** UI와 파일 I/O를 분리했고, 알고리즘은 나중에 C++로 끼울 자리만 열어 두었다.  
- **BMP:** 헤더를 직접 파싱해 크기·비트깊이를 확인한 뒤 로드한다. 5.6GB는 Preview로 표시한다.  
- **다음 주:** ROI 픽셀 추출, Histogram 실제 계산, Dilation/Erosion/Threshold를 C++로 구현한다.
