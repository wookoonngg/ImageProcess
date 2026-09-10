#pragma once

// ImageProcessingNative — C API for WPF P/Invoke
// WEEK 2: signatures only / empty stubs. Algorithms filled later (no OpenCV).
//
// Buffer convention:
//   - 8-bit grayscale, row-major, no row padding (stride == width)
//   - ROI is inclusive of (x,y) with size (width,height) in image coordinates
//
// Return codes: 0 = OK, negative = error (see IpStatus)

#ifdef IMAGEPROCESSINGNATIVE_EXPORTS
#define IP_API __declspec(dllexport)
#else
#define IP_API __declspec(dllimport)
#endif

#ifdef __cplusplus
extern "C" {
#endif

enum IpStatus
{
    IP_OK = 0,
    IP_ERR_NULL_PTR = -1,
    IP_ERR_INVALID_SIZE = -2,
    IP_ERR_INVALID_PARAM = -3,
    IP_ERR_NOT_IMPLEMENTED = -100
};

typedef struct IpRoi
{
    int X;
    int Y;
    int Width;
    int Height;
} IpRoi;

typedef struct IpMatchResult
{
    int BestX;
    int BestY;
    double Score;
} IpMatchResult;

// ----- Morphology -----
IP_API int IpDilation(const unsigned char* src, unsigned char* dst, int width, int height, int kernelSize, const IpRoi* roi);
IP_API int IpErosion(const unsigned char* src, unsigned char* dst, int width, int height, int kernelSize, const IpRoi* roi);






// ----- Smoothing / Threshold -----
IP_API int IpSmoothing(const unsigned char* src, unsigned char* dst, int width, int height, int kernelSize, const IpRoi* roi);
IP_API int IpThreshold(const unsigned char* src, unsigned char* dst, int width, int height, int thresholdValue, const IpRoi* roi);



// ----- Filters -----
IP_API int IpGaussian(const unsigned char* src, unsigned char* dst, int width, int height, int kernelSize, double sigma, const IpRoi* roi);
IP_API int IpLaplacian(const unsigned char* src, unsigned char* dst, int width, int height, const IpRoi* roi);
IP_API int IpSobel(const unsigned char* src, unsigned char* dst, int width, int height, const IpRoi* roi);



// ----- Template Matching -----
// method: 0=DIFF, 1=CORR, 2=COEFF
IP_API int IpTemplateMatch(
    const unsigned char* image, int imageWidth, int imageHeight,
    const unsigned char* templ, int templWidth, int templHeight,
    int method,
    IpMatchResult* outResult,
    const IpRoi* searchRoi);




// ----- ROI helpers -----
IP_API int IpExtractRoi(

    const unsigned char* src, int srcWidth, int srcHeight,
    const IpRoi* roi,
    unsigned char* dst, int dstCapacityBytes);




IP_API int IpComputeHistogram(

    const unsigned char* src, int width, int height,
    const IpRoi* roi,
    int* bins256);

#ifdef __cplusplus
}
#endif
