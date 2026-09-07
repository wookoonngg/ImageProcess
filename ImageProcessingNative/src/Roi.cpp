#include "ImageProcessingApi.h"

// WEEK 2 stub — copy ROI pixels later
int IpExtractRoi(
    const unsigned char* src, int srcWidth, int srcHeight,
    const IpRoi* roi,
    unsigned char* dst, int dstCapacityBytes)
{
    (void)src; (void)srcWidth; (void)srcHeight; (void)roi; (void)dst; (void)dstCapacityBytes;
    return IP_ERR_NOT_IMPLEMENTED;
}

// WEEK 2 stub — 256-bin histogram later
int IpComputeHistogram(
    const unsigned char* src, int width, int height,
    const IpRoi* roi,
    int* bins256)
{
    (void)src; (void)width; (void)height; (void)roi; (void)bins256;
    return IP_ERR_NOT_IMPLEMENTED;
}
