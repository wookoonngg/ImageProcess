#include "ImageProcessingApi.h"
#include "Common.hpp"
#include <algorithm>
#include <cmath>
#include <cfloat>

#if defined(_MSC_VER)
#include <intrin.h>
#endif

static double ScoreDiff(const unsigned char* image, int imageWidth, const unsigned char* templ, int templWidth, int templHeight, int ox, int oy)
{
    const double area = static_cast<double>(templWidth) * templHeight;
    long long sum = 0;

    for (int ty = 0; ty < templHeight; ++ty)
    {
        const unsigned char* irow = image + (oy + ty) * imageWidth + ox;
        const unsigned char* trow = templ + ty * templWidth;
        int tx = 0;

#if defined(_MSC_VER)
        __m128i acc = _mm_setzero_si128();
        const __m128i z = _mm_setzero_si128();
        for (; tx + 16 <= templWidth; tx += 16)
        {
            __m128i a = _mm_loadu_si128((const __m128i*)(irow + tx));
            __m128i b = _mm_loadu_si128((const __m128i*)(trow + tx));
            __m128i d0 = _mm_sub_epi16(_mm_unpacklo_epi8(a, z), _mm_unpacklo_epi8(b, z));
            __m128i d1 = _mm_sub_epi16(_mm_unpackhi_epi8(a, z), _mm_unpackhi_epi8(b, z));
            acc = _mm_add_epi32(acc, _mm_madd_epi16(d0, d0));
            acc = _mm_add_epi32(acc, _mm_madd_epi16(d1, d1));
        }
        acc = _mm_add_epi32(acc, _mm_srli_si128(acc, 8));
        acc = _mm_add_epi32(acc, _mm_srli_si128(acc, 4));
        sum += _mm_cvtsi128_si32(acc);
#endif
        for (; tx < templWidth; ++tx)
        {
            int d = (int)irow[tx] - (int)trow[tx];
            sum += d * d;
        }
    }
    return 1.0 / (1.0 + (double)sum / area);
}

static double ScoreCorr(const unsigned char* image, int imageWidth, const unsigned char* templ, int templWidth, int templHeight, int ox, int oy)
{
    long long sumIT = 0, sumI2 = 0, sumT2 = 0;
    for (int ty = 0; ty < templHeight; ++ty)
    {
        const unsigned char* irow = image + (oy + ty) * imageWidth + ox;
        const unsigned char* trow = templ + ty * templWidth;
        int tx = 0;
#if defined(_MSC_VER)
        __m128i aIT = _mm_setzero_si128(), aI2 = _mm_setzero_si128(), aT2 = _mm_setzero_si128();
        const __m128i z = _mm_setzero_si128();
        for (; tx + 16 <= templWidth; tx += 16)
        {
            __m128i a = _mm_loadu_si128((const __m128i*)(irow + tx));
            __m128i b = _mm_loadu_si128((const __m128i*)(trow + tx));
            __m128i loA = _mm_unpacklo_epi8(a, z), loB = _mm_unpacklo_epi8(b, z);
            __m128i hiA = _mm_unpackhi_epi8(a, z), hiB = _mm_unpackhi_epi8(b, z);
            aIT = _mm_add_epi32(aIT, _mm_add_epi32(_mm_madd_epi16(loA, loB), _mm_madd_epi16(hiA, hiB)));
            aI2 = _mm_add_epi32(aI2, _mm_add_epi32(_mm_madd_epi16(loA, loA), _mm_madd_epi16(hiA, hiA)));
            aT2 = _mm_add_epi32(aT2, _mm_add_epi32(_mm_madd_epi16(loB, loB), _mm_madd_epi16(hiB, hiB)));
        }
        aIT = _mm_add_epi32(aIT, _mm_srli_si128(aIT, 8)); aIT = _mm_add_epi32(aIT, _mm_srli_si128(aIT, 4));
        aI2 = _mm_add_epi32(aI2, _mm_srli_si128(aI2, 8)); aI2 = _mm_add_epi32(aI2, _mm_srli_si128(aI2, 4));
        aT2 = _mm_add_epi32(aT2, _mm_srli_si128(aT2, 8)); aT2 = _mm_add_epi32(aT2, _mm_srli_si128(aT2, 4));
        sumIT += _mm_cvtsi128_si32(aIT); sumI2 += _mm_cvtsi128_si32(aI2); sumT2 += _mm_cvtsi128_si32(aT2);
#endif
        for (; tx < templWidth; ++tx)
        {
            long long iv = irow[tx], tv = trow[tx];
            sumIT += iv * tv; sumI2 += iv * iv; sumT2 += tv * tv;
        }
    }
    double denom = std::sqrt((double)sumI2 * (double)sumT2);
    return (denom < 1e-9) ? 0.0 : (double)sumIT / denom;
}

static double ScoreCoeff(const unsigned char* image, int imageWidth, const unsigned char* templ, int templWidth, int templHeight, int ox, int oy)
{
    const double area = (double)templWidth * templHeight;
    double sumI = 0.0, sumT = 0.0;
    for (int ty = 0; ty < templHeight; ++ty)
        for (int tx = 0; tx < templWidth; ++tx)
        {
            sumI += image[(oy + ty) * imageWidth + (ox + tx)];
            sumT += templ[ty * templWidth + tx];
        }
    const double meanI = sumI / area, meanT = sumT / area;
    double num = 0.0, denI = 0.0, denT = 0.0;
    for (int ty = 0; ty < templHeight; ++ty)
        for (int tx = 0; tx < templWidth; ++tx)
        {
            double di = image[(oy + ty) * imageWidth + (ox + tx)] - meanI;
            double dt = templ[ty * templWidth + tx] - meanT;
            num += di * dt;
            denI += di * di;
            denT += dt * dt;
        }
    double denom = std::sqrt(denI * denT);
    return (denom < 1e-9) ? 0.0 : num / denom;
}

int IpTemplateMatch(const unsigned char* image, int imageWidth, int imageHeight,
    const unsigned char* templ, int templWidth, int templHeight,
    int method, IpMatchResult* outResult, const IpRoi* searchRoi)
{
    if (image == nullptr || templ == nullptr || outResult == nullptr)
        return IP_ERR_NULL_PTR;
    if (imageWidth <= 0 || imageHeight <= 0 || templWidth <= 0 || templHeight <= 0)
        return IP_ERR_INVALID_SIZE;
    if (templWidth > imageWidth || templHeight > imageHeight || method < 0 || method > 2)
        return IP_ERR_INVALID_PARAM;

    int startX = 0, startY = 0;
    int endX = imageWidth - templWidth + 1;
    int endY = imageHeight - templHeight + 1;
    if (searchRoi != nullptr)
    {
        startX = std::max(0, searchRoi->X);
        startY = std::max(0, searchRoi->Y);
        endX = std::min(endX, searchRoi->X + searchRoi->Width - templWidth + 1);
        endY = std::min(endY, searchRoi->Y + searchRoi->Height - templHeight + 1);
    }
    if (startX >= endX || startY >= endY)
        return IP_ERR_INVALID_PARAM;

    double bestScore = -DBL_MAX;
    int bestX = startX, bestY = startY;

#pragma omp parallel
    {
        double localBest = -DBL_MAX;
        int lx = startX, ly = startY;
#pragma omp for schedule(static) nowait
        for (int y = startY; y < endY; ++y)
        {
            for (int x = startX; x < endX; ++x)
            {
                double score = 0.0;
                if (method == 0) score = ScoreDiff(image, imageWidth, templ, templWidth, templHeight, x, y);
                else if (method == 1) score = ScoreCorr(image, imageWidth, templ, templWidth, templHeight, x, y);
                else score = ScoreCoeff(image, imageWidth, templ, templWidth, templHeight, x, y);
                if (score > localBest) { localBest = score; lx = x; ly = y; }
            }
        }
#pragma omp critical
        if (localBest > bestScore) { bestScore = localBest; bestX = lx; bestY = ly; }
    }

    outResult->BestX = bestX;
    outResult->BestY = bestY;
    outResult->Score = bestScore;
    return IP_OK;
}
