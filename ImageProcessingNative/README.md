# ImageProcessingNative

WPF `ImageProgram.exe`와 같은 폴더의 `ImageProcessingNative.dll` 로 로드됩니다.

## 연산 파일

| 파일 | 내용 |
|------|------|
| Morphology.cpp | 팽창 / 수축 |
| Smoothing.cpp | 평활화 (box mean) |
| Threshold.cpp | 이진화 |
| Filter.cpp | Gaussian / Laplacian / Sobel |
| TemplateMatching.cpp | DIFF / CORR / COEFF |
| Roi.cpp | ROI 추출 / Histogram |
| include/Common.hpp | stride·ROI 공통 유틸 |

버퍼 계약: **8bit gray, stride == width** (C# `PixelBuffer`와 동일)

## 빌드

Visual Studio에서 `ImageProcessingNative.vcxproj` (x64 / Debug|Release) 빌드.
