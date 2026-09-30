#include "ImageProcessingApi.h"
#include "Common.hpp"
#include <algorithm>
#include <cmath>

#if defined(_MSC_VER)
#include <intrin.h>
#endif

int IpSmoothing(const unsigned char* src, unsigned char* dst, int width, int height, int kernelSize, const IpRoi* roi)
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

    const float invArea = 1.0f / static_cast<float>(kernelSize * kernelSize);
    const int area = kernelSize * kernelSize;





    // epi16 합 overflow 방지: 255 * area <= 65535
    const bool useSimd = (area <= 255);

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
        if (useSimd && rowSafe)
        {
            const __m128i zero = _mm_setzero_si128();
            const __m128 inv = _mm_set1_ps(invArea);

            for (; x < safeStartX; ++x)
            {
                float sum = 0.0f;
                for (int ky = -radius; ky <= radius; ++ky)
                    for (int kx = -radius; kx <= radius; ++kx)
                        sum += IpSample(src, x + kx, y + ky, width, height);
                dst[y * width + x] = static_cast<unsigned char>(std::clamp(sum * invArea + 0.5f, 0.0f, 255.0f));
            }

            for (; x + 16 <= safeEndX; x += 16)
            {
                __m128i sumLo = _mm_setzero_si128();
                __m128i sumHi = _mm_setzero_si128();

                for (int ky = -radius; ky <= radius; ++ky)
                {
                    const unsigned char* row = src + (y + ky) * width;
                    for (int kx = -radius; kx <= radius; ++kx)
                    {
                        __m128i v = _mm_loadu_si128(reinterpret_cast<const __m128i*>(row + x + kx));
                        sumLo = _mm_add_epi16(sumLo, _mm_unpacklo_epi8(v, zero));
                        sumHi = _mm_add_epi16(sumHi, _mm_unpackhi_epi8(v, zero));
                    }
                }

                // 8×uint16 → float → round → 8×int16
                __m128i i0 = _mm_unpacklo_epi16(sumLo, zero);
                __m128i i1 = _mm_unpackhi_epi16(sumLo, zero);
                __m128i i2 = _mm_unpacklo_epi16(sumHi, zero);
                __m128i i3 = _mm_unpackhi_epi16(sumHi, zero);

                __m128i r0 = _mm_cvtps_epi32(_mm_mul_ps(_mm_cvtepi32_ps(i0), inv));
                __m128i r1 = _mm_cvtps_epi32(_mm_mul_ps(_mm_cvtepi32_ps(i1), inv));
                __m128i r2 = _mm_cvtps_epi32(_mm_mul_ps(_mm_cvtepi32_ps(i2), inv));
                __m128i r3 = _mm_cvtps_epi32(_mm_mul_ps(_mm_cvtepi32_ps(i3), inv));

                __m128i p01 = _mm_packs_epi32(r0, r1);
                __m128i p23 = _mm_packs_epi32(r2, r3);
                _mm_storeu_si128(reinterpret_cast<__m128i*>(dst + y * width + x),
                    _mm_packus_epi16(p01, p23));
            }
        }
#endif
        for (; x < endX; ++x)
        {
            float sum = 0.0f;
            for (int ky = -radius; ky <= radius; ++ky)
                for (int kx = -radius; kx <= radius; ++kx)
                    sum += IpSample(src, x + kx, y + ky, width, height);
            dst[y * width + x] = static_cast<unsigned char>(std::clamp(sum * invArea + 0.5f, 0.0f, 255.0f));
        }
    }
    return IP_OK;
}
