#include "ImageProcessingApi.h"
#include "Common.hpp"
#include <algorithm>
#include <cmath>
#include <vector>

int IpSmoothing(const unsigned char* src, unsigned char* dst, int width, int height, int kernelSize, const IpRoi* roi)
{
    if (!IpValidate(src, dst, width, height))
        return IP_ERR_NULL_PTR;

    kernelSize = IpClampKernel(kernelSize); // 커널 사이즈 보정 

    const int radius = kernelSize / 2; // 커널 영역
    int startX, startY, endX, endY;

    IpResolveRoi(roi, width, height, startX, startY, endX, endY);
    IpCopyImage(src, dst, width, height);

    // 3x3 Gaussian-ish mean (노이즈 제거용 box blur)
    const float invArea = 1.0f / static_cast<float>(kernelSize * kernelSize);

    for (int y = startY; y < endY; ++y)
    {
        for (int x = startX; x < endX; ++x)
        {
            float sum = 0.0f;
            for (int ky = -radius; ky <= radius; ++ky)
            {
                for (int kx = -radius; kx <= radius; ++kx)
                    sum += IpSample(src, x + kx, y + ky, width, height);
            }



            dst[y * width + x] = static_cast<unsigned char>(std::clamp(sum * invArea + 0.5f, 0.0f, 255.0f));
        }
    }
    return IP_OK;
}
