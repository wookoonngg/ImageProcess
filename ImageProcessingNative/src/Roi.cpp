#include "ImageProcessingApi.h"
#include "Common.hpp"
#include <cstring>

int IpExtractRoi(
    const unsigned char* src, int srcWidth, int srcHeight,
    const IpRoi* roi,
    unsigned char* dst, int dstCapacityBytes)
{
    if (src == nullptr || roi == nullptr || dst == nullptr)
        return IP_ERR_NULL_PTR;
    if (srcWidth <= 0 || srcHeight <= 0 || roi->Width <= 0 || roi->Height <= 0)
        return IP_ERR_INVALID_SIZE;

    const int needed = roi->Width * roi->Height;
    if (dstCapacityBytes < needed)
        return IP_ERR_INVALID_SIZE;

    for (int y = 0; y < roi->Height; ++y)
    {
        for (int x = 0; x < roi->Width; ++x)
            dst[y * roi->Width + x] = IpSample(src, roi->X + x, roi->Y + y, srcWidth, srcHeight);
    }
    return IP_OK;
}

int IpComputeHistogram(
    const unsigned char* src, int width, int height,
    const IpRoi* roi,
    int* bins256)
{
    if (src == nullptr || bins256 == nullptr)
        return IP_ERR_NULL_PTR;
    if (width <= 0 || height <= 0)
        return IP_ERR_INVALID_SIZE;

    std::memset(bins256, 0, sizeof(int) * 256);

    int startX, startY, endX, endY;
    IpResolveRoi(roi, width, height, startX, startY, endX, endY);

    for (int y = startY; y < endY; ++y)
    {
        const unsigned char* row = src + y * width;
        for (int x = startX; x < endX; ++x)
            ++bins256[row[x]];
    }
    return IP_OK;
}
