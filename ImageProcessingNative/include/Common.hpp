#pragma once

#include "ImageProcessingApi.h"
#include <algorithm>
#include <cstring>

// C# 픽셀 버퍼 패딩 없이 == width -> 일대일 매칭 되게
inline int IpStride(int width) { return width; }

inline bool IpValidate(const unsigned char* src, unsigned char* dst, int width, int height)
{
    if (src == nullptr || dst == nullptr)
        return false;
    if (width <= 0 || height <= 0)
        return false;
    return true;
}

inline void IpResolveRoi(const IpRoi* roi, int width, int height, int& startX, int& startY, int& endX, int& endY)
{
    // roi 널값이면 전체이미ㅣ지로
    startX = 0;
    startY = 0;
    endX = width;
    endY = height;

    if (roi == nullptr)
        return;

    startX = std::max(0, roi->X);
    startY = std::max(0, roi->Y);
    endX = std::min(width, roi->X + roi->Width);
    endY = std::min(height, roi->Y + roi->Height);



    // Invalid ROI: empty range (do NOT expand to full image)
    if (startX >= endX || startY >= endY)
    {
        startX = 0;
        startY = 0;
        endX = 0;
        endY = 0;
    }
}

inline void IpCopyImage(const unsigned char* src, unsigned char* dst, int width, int height)
{
    std::memcpy(dst, src, static_cast<size_t>(width) * static_cast<size_t>(height));
}

inline unsigned char IpSample(const unsigned char* src, int x, int y, int width, int height)
{
    x = std::clamp(x, 0, width - 1);
    y = std::clamp(y, 0, height - 1);
    return src[y * width + x];
}

inline int IpClampKernel(int kernelSize)
{
    if (kernelSize < 1)
        return 1;
    if ((kernelSize & 1) == 0)
        ++kernelSize;
    return kernelSize;
}
