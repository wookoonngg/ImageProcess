#include "ImageProcessingApi.h"
#include "Common.hpp"
#include <algorithm>

int IpDilation(const unsigned char* src, unsigned char* dst, int width, int height, int kernelSize, const IpRoi* roi)
{
    if (!IpValidate(src, dst, width, height))
        return IP_ERR_NULL_PTR;

    kernelSize = IpClampKernel(kernelSize);
    const int radius = kernelSize / 2;
    int startX, startY, endX, endY;
    IpResolveRoi(roi, width, height, startX, startY, endX, endY);
    IpCopyImage(src, dst, width, height);

    for (int y = startY; y < endY; ++y)
    {
        for (int x = startX; x < endX; ++x)
        {
            unsigned char maxVal = 0;
            for (int ky = -radius; ky <= radius; ++ky)
            {
                for (int kx = -radius; kx <= radius; ++kx)
                    maxVal = std::max(maxVal, IpSample(src, x + kx, y + ky, width, height));
            }
            dst[y * width + x] = maxVal;
        }
    }
    return IP_OK;
}

int IpErosion(const unsigned char* src, unsigned char* dst, int width, int height, int kernelSize, const IpRoi* roi)
{
    if (!IpValidate(src, dst, width, height))
        return IP_ERR_NULL_PTR;

    kernelSize = IpClampKernel(kernelSize);
    const int radius = kernelSize / 2;
    int startX, startY, endX, endY;
    IpResolveRoi(roi, width, height, startX, startY, endX, endY);
    IpCopyImage(src, dst, width, height);

    for (int y = startY; y < endY; ++y)
    {
        for (int x = startX; x < endX; ++x)
        {
            unsigned char minVal = 255;
            for (int ky = -radius; ky <= radius; ++ky)
            {
                for (int kx = -radius; kx <= radius; ++kx)
                    minVal = std::min(minVal, IpSample(src, x + kx, y + ky, width, height));
            }
            dst[y * width + x] = minVal;
        }
    }
    return IP_OK;
}
