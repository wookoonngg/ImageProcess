# ImageProcessingNative — C++ 영상처리 DLL (WEEK 2 골격)

연산 구현은 `src/` 아래 파일에만 추가하면 됩니다. 시그니처는 `include/ImageProcessingApi.h`.

| 파일 | 담당 |
|------|------|
| Morphology.cpp | 팽창 / 수축 |
| Smoothing.cpp | 평활화 |
| Threshold.cpp | 이진화 |
| Filter.cpp | Gaussian / Laplacian / Sobel |
| TemplateMatching.cpp | DIFF / CORR / COEFF |
| Roi.cpp | ROI 추출 / Histogram |

빌드: Visual Studio에서 `ImageProcessingNative.vcxproj` (x64) → 산출물 `ImageProcessingNative.dll` 이 WPF exe 옆에 복사됨.
