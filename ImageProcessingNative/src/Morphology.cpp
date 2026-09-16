#include "ImageProcessingApi.h"
#include "Common.hpp"
#include <algorithm>

int IpDilation(const unsigned char* src, unsigned char* dst, int width, int height, int kernelSize, const IpRoi* roi)
{


    //예외처리 null 값들어오거나 영상 크기 이상한거 (0기준 ,w<h)

    if (!IpValidate(src, dst, width, height))
        return IP_ERR_NULL_PTR;


    // 커널 사이즈 짜수로 보정 
    kernelSize = IpClampKernel(kernelSize);
    const int radius = kernelSize / 2; // 반지름 구하기 

    int startX, startY, endX, endY;
    IpResolveRoi(roi, width, height, startX, startY, endX, endY); // roi 구하기 살펴볼 영역 정함
    IpCopyImage(src, dst, width, height); // 원본 복사


    //x,y 이미지 전체 순회 하면서 max 값으로 ipSample 통과시켜서 갱신 
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





// 위에랑 똑같이 min



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
                    // min 값으로 교체
            }
            dst[y * width + x] = minVal;
        }
    }
    return IP_OK;
}
