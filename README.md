# WPF 반도체 검사 영상처리 프로그램

4주 OJT 프로젝트 — WEEK 1: BMP Viewer 기반 구축

## WEEK 1 구현 기능

- BMP Open / Save
- Image Viewer 1 (원본)
- Image Viewer 2 (처리 결과 — 현재는 원본 복사본)
- Navigator (전체 미리보기 + 현재 뷰포트 표시)
- 마우스 휠 확대/축소, 드래그 이동
- BMP 헤더 직접 파싱 (`ReadHeader`)
- UI / 영상처리 로직 분리 (Service, Model, Control)

## 프로젝트 구조

```text
ImageProgram/
├── App.xaml / MainWindow.xaml     # WPF 진입점, 메인 UI
├── Controls/
│   ├── ImageViewerControl         # 이미지 표시 + Zoom/Pan
│   └── NavigatorControl           # 미리보기 + 뷰포트
├── Models/
│   ├── ImageData.cs               # 메모리 상 이미지 데이터
│   └── BitmapFileInfo.cs          # BMP 헤더 정보
├── Services/
│   ├── ImageFileService.cs        # BMP I/O
│   └── ConsoleLogger.cs
└── Utils/
    ├── ImageConverter.cs          # Bitmap ↔ BitmapImage
    ├── ValidationHelper.cs
    └── Constants.cs
```

## 실행 방법

```bash
dotnet build ImageProgram.sln
dotnet run --project ImageProgram/ImageProgram.csproj
```

Visual Studio에서 `ImageProgram.sln` 열기 → F5

## 테스트

1. **파일 → 열기** 로 BMP 선택
2. Viewer 1에서 **휠** = 확대/축소, **드래그** = 이동
3. Navigator에서 하늘색 사각형 = 현재 보이는 영역
4. **파일 → 저장** 으로 결과 Viewer 2 이미지 BMP 저장

### 테스트 이미지 주의

`Resource/TestImages/Foundry_4umDF 1.bmp` 는 **약 5.6GB** 입니다.

- `ReadHeader()` 로 크기/비트깊이 확인 가능
- 전체 로드는 RAM 부족(OOM) 가능 → **발표 Demo용으로는 작은 BMP(수 MB) 권장**
- WEEK 4에서 Tile 기반 로딩 검토

## 발표 포인트 (WEEK 1)

| 주제 | 설명 |
|------|------|
| WPF 선택 이유 | XAML UI + C# 로직 분리, 장비 SW GUI에 적합 |
| Viewer 분리 | 원본(Viewer1) vs 처리결과(Viewer2) — 검사 SW와 동일 패턴 |
| BMP 흐름 | 파일 → 헤더 파싱 → Bitmap 로드 → BitmapImage 변환 → WPF 표시 |
| 좌표계 | 화면 좌표(ScrollViewer) vs 이미지 좌표 — WEEK 2 ROI에서 본격 사용 |

## 다음 주 (WEEK 2) 계획

- ROI Select / Cancel
- Histogram Viewer
- Dilation, Erosion, Smoothing, Threshold
- Processing Time UI
- **C++ 영상처리 DLL** 프로젝트 추가 (과제 조건)
