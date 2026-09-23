#include "ImageProcessingApi.h"
#include "Common.hpp"
#include <algorithm>
#include <cstring>

#if defined(_MSC_VER)
#include <intrin.h>
#endif

int IpThreshold(const unsigned char* src, unsigned char* dst, int width, int height, int thresholdValue, const IpRoi* roi)
{
    if (!IpValidate(src, dst, width, height))
        return IP_ERR_NULL_PTR;

    thresholdValue = std::clamp(thresholdValue, 0, 255);
    int startX, startY, endX, endY;
    IpResolveRoi(roi, width, height, startX, startY, endX, endY);
    IpCopyImage(src, dst, width, height);

    if (startX >= endX || startY >= endY)
        return IP_OK;

    const unsigned char t = static_cast<unsigned char>(thresholdValue);

#if defined(_MSC_VER)
    const __m128i threshVec = _mm_set1_epi8(static_cast<char>(t));
#endif

#pragma omp parallel for schedule(static) if((endY - startY) * (endX - startX) > 4096)
    for (int y = startY; y < endY; ++y)
    {
        int x = startX;
        const int row = y * width;

#if defined(_MSC_VER)
        for (; x + 16 <= endX; x += 16)
        {
            __m128i v = _mm_loadu_si128(reinterpret_cast<const __m128i*>(src + row + x));
            __m128i minv = _mm_min_epu8(v, threshVec);
            __m128i ge = _mm_cmpeq_epi8(minv, threshVec);
            _mm_storeu_si128(reinterpret_cast<__m128i*>(dst + row + x), ge);
        }
#endif
        for (; x < endX; ++x)
        {
            const int index = row + x;
            dst[index] = (src[index] >= t) ? 255 : 0;
        }
    }
    return IP_OK;
}
