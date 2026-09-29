#include "ImageProcessingApi.h"
#include "Common.hpp"
#include <algorithm>

#if defined(_MSC_VER)
#include <intrin.h>
#endif

int IpDilation(const unsigned char* src, unsigned char* dst, int width, int height, int kernelSize, const IpRoi* roi)
{
    if (!IpValidate(src, dst, width, height))
        return IP_ERR_NULL_PTR;

    kernelSize = IpClampKernel(kernelSize);
    const int radius = kernelSize / 2;

    int startX, startY, endX, endY;
    IpResolveRoi(roi, width, height, startX, startY, endX, endY);
    IpCopyImage(src, dst, width, height);

    if (startX >= endX || startY >= endY)
        return IP_OK;

    // 경계 clamp 없이 이웃을 읽을 수 있는 내부 구간 (SIMD용)
    const int safeStartX = std::max(startX, radius);
    const int safeEndX = std::min(endX, width - radius);
    const int safeStartY = std::max(startY, radius);
    const int safeEndY = std::min(endY, height - radius);

#pragma omp parallel for schedule(static) if((endY - startY) * (endX - startX) > 2048)
    for (int y = startY; y < endY; ++y)
    {
        int x = startX;
        const bool rowSafe = (y >= safeStartY && y < safeEndY);

#if defined(_MSC_VER)
        if (rowSafe)
        {
            for (; x < safeStartX; ++x)
            {
                unsigned char maxVal = 0;
                for (int ky = -radius; ky <= radius; ++ky)
                    for (int kx = -radius; kx <= radius; ++kx)
                        maxVal = std::max(maxVal, IpSample(src, x + kx, y + ky, width, height));
                dst[y * width + x] = maxVal;
            }

            for (; x + 16 <= safeEndX; x += 16)
            {
                __m128i acc = _mm_setzero_si128();
                for (int ky = -radius; ky <= radius; ++ky)
                {
                    const unsigned char* row = src + (y + ky) * width;
                    for (int kx = -radius; kx <= radius; ++kx)
                    {
                        __m128i v = _mm_loadu_si128(reinterpret_cast<const __m128i*>(row + x + kx));
                        acc = _mm_max_epu8(acc, v);
                    }
                }
                _mm_storeu_si128(reinterpret_cast<__m128i*>(dst + y * width + x), acc);
            }
        }
#endif
        for (; x < endX; ++x)
        {
            unsigned char maxVal = 0;
            for (int ky = -radius; ky <= radius; ++ky)
                for (int kx = -radius; kx <= radius; ++kx)
                    maxVal = std::max(maxVal, IpSample(src, x + kx, y + ky, width, height));
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

    if (startX >= endX || startY >= endY)
        return IP_OK;

    const int safeStartX = std::max(startX, radius);
    const int safeEndX = std::min(endX, width - radius);
    const int safeStartY = std::max(startY, radius);
    const int safeEndY = std::min(endY, height - radius);

#pragma omp parallel for schedule(static) if((endY - startY) * (endX - startX) > 2048)
    for (int y = startY; y < endY; ++y)
    {
        int x = startX;
        const bool rowSafe = (y >= safeStartY && y < safeEndY);

#if defined(_MSC_VER)
        if (rowSafe)
        {
            for (; x < safeStartX; ++x)
            {
                unsigned char minVal = 255;
                for (int ky = -radius; ky <= radius; ++ky)
                    for (int kx = -radius; kx <= radius; ++kx)
                        minVal = std::min(minVal, IpSample(src, x + kx, y + ky, width, height));
                dst[y * width + x] = minVal;
            }

            for (; x + 16 <= safeEndX; x += 16)
            {
                __m128i acc = _mm_set1_epi8(static_cast<char>(0xFF));
                for (int ky = -radius; ky <= radius; ++ky)
                {
                    const unsigned char* row = src + (y + ky) * width;
                    for (int kx = -radius; kx <= radius; ++kx)
                    {
                        __m128i v = _mm_loadu_si128(reinterpret_cast<const __m128i*>(row + x + kx));
                        acc = _mm_min_epu8(acc, v);
                    }
                }
                _mm_storeu_si128(reinterpret_cast<__m128i*>(dst + y * width + x), acc);
            }
        }
#endif
        for (; x < endX; ++x)
        {
            unsigned char minVal = 255;
            for (int ky = -radius; ky <= radius; ++ky)
                for (int kx = -radius; kx <= radius; ++kx)
                    minVal = std::min(minVal, IpSample(src, x + kx, y + ky, width, height));
            dst[y * width + x] = minVal;
        }
    }
    return IP_OK;
}
