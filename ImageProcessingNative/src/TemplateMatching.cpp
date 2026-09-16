#include "ImageProcessingApi.h"
#include "Common.hpp"
#include <algorithm>
#include <cmath>
#include <cfloat>



// template과 원본 차이 계산 

static double ScoreDiff(const unsigned char* image, int imageWidth, const unsigned char* templ, int templWidth, int templHeight, int ox, int oy)
{


    double sum = 0.0; 


    const double area = static_cast<double>(templWidth) * templHeight;

    for (int ty = 0; ty < templHeight; ++ty)
    {
        for (int tx = 0; tx < templWidth; ++tx)
        {
            const int diff = static_cast<int>(image[(oy + ty) * imageWidth + (ox + tx)]) - static_cast<int>(templ[ty * templWidth + tx]); // 걍 빼고

            // 뺀건 제곱 - 음수 없애기 혹시나 

            sum += diff * diff;


        }
    }
    // 낮을수록 유사 → 점수는 1 / (1 + meanSSE)
    return 1.0 / (1.0 + sum / area);
}

static double ScoreCorr( const unsigned char* image, int imageWidth, const unsigned char* templ, int templWidth, int templHeight, int ox, int oy)
{


    double sumIT = 0.0;
    double sumI2 = 0.0;
    double sumT2 = 0.0;


    for (int ty = 0; ty < templHeight; ++ty)
    {
        for (int tx = 0; tx < templWidth; ++tx)
        {


            const double iv = image[(oy + ty) * imageWidth + (ox + tx)];
            const double tv = templ[ty * templWidth + tx];
            sumIT += iv * tv;
            sumI2 += iv * iv;
            sumT2 += tv * tv;
        }
    }
    const double denom = std::sqrt(sumI2 * sumT2);
    if (denom < 1e-9)
        return 0.0;
    return sumIT / denom;
}

static double ScoreCoeff(
    const unsigned char* image, int imageWidth,
    const unsigned char* templ, int templWidth, int templHeight,
    int ox, int oy)
{
    const double area = static_cast<double>(templWidth) * templHeight;
    double sumI = 0.0, sumT = 0.0;
    for (int ty = 0; ty < templHeight; ++ty)
    {
        for (int tx = 0; tx < templWidth; ++tx)
        {
            sumI += image[(oy + ty) * imageWidth + (ox + tx)];
            sumT += templ[ty * templWidth + tx];
        }
    }
    const double meanI = sumI / area;
    const double meanT = sumT / area;

    double num = 0.0, denI = 0.0, denT = 0.0;
    for (int ty = 0; ty < templHeight; ++ty)
    {
        for (int tx = 0; tx < templWidth; ++tx)
        {
            const double di = image[(oy + ty) * imageWidth + (ox + tx)] - meanI;
            const double dt = templ[ty * templWidth + tx] - meanT;
            num += di * dt;
            denI += di * di;
            denT += dt * dt;
        }
    }
    const double denom = std::sqrt(denI * denT);
    if (denom < 1e-9)
        return 0.0;
    return num / denom;
}

int IpTemplateMatch(const unsigned char* image, int imageWidth, int imageHeight, const unsigned char* templ, int templWidth, int templHeight,int method, IpMatchResult* outResult,const IpRoi* searchRoi)
{
    if (image == nullptr || templ == nullptr || outResult == nullptr)
        return IP_ERR_NULL_PTR;
    if (imageWidth <= 0 || imageHeight <= 0 || templWidth <= 0 || templHeight <= 0)
        return IP_ERR_INVALID_SIZE;
    if (templWidth > imageWidth || templHeight > imageHeight)
    if (method < 0 || method > 2)
        return IP_ERR_INVALID_PARAM;
        return IP_ERR_INVALID_PARAM;




    int startX = 0;
    int startY = 0;

    // 끝지점 걍 끝으로 하면 템플릿 넘어감 그만큼 빼고 끝점 지정

    int endX = imageWidth - templWidth + 1;
    int endY = imageHeight - templHeight + 1;


    //roi가 있을 때 
    if (searchRoi != nullptr)
    {

        startX = std::max(0, searchRoi->X); // roi 영역 내에서만 탐색
        startY = std::max(0, searchRoi->Y);
        endX = std::min(endX, searchRoi->X + searchRoi->Width - templWidth + 1);
        endY = std::min(endY, searchRoi->Y + searchRoi->Height - templHeight + 1);
    }



    if (startX >= endX || startY >= endY)
        return IP_ERR_INVALID_PARAM;


    //가장 좋은 최적 값 저장
    double bestScore = -DBL_MAX;

    // X,Y엔 최적 위치 저장 
    int bestX = startX;
    int bestY = startY;


    // 슬라이딩 템플릿을 움직이면서

    for (int y = startY; y < endY; ++y)
    {
        for (int x = startX; x < endX; ++x)
        {
            //매 위치마다 유사도 계산 
            double score = 0.0;
            switch (method)
            {
            case 0: score = ScoreDiff(image, imageWidth, templ, templWidth, templHeight, x, y); break;
            case 1: score = ScoreCorr(image, imageWidth, templ, templWidth, templHeight, x, y); break;
            case 2: score = ScoreCoeff(image, imageWidth, templ, templWidth, templHeight, x, y); break;
            default: break;
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestX = x;
                bestY = y;
            }
        }
    }

    outResult->BestX = bestX;
    outResult->BestY = bestY;
    outResult->Score = bestScore;
    return IP_OK;
}
