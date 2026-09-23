#include "ImageProcessingApi.h"
#include "Common.hpp"
#include <algorithm>
#include <cmath>
#include <vector>



// 커널 만들기 필터를 만드는 필터링은 아ㅣㄴ고 
static void BuildGaussianKernel(int kernelSize, double sigma, std::vector<float>& kernel)
{   

    // 커널 크기 보정
    kernelSize = IpClampKernel(kernelSize);
    const int radius = kernelSize / 2; // 커널 영역

    // 시그마는 분산된 정도 

    if (sigma <= 0.0)
        sigma = std::max(0.5, kernelSize / 6.0);



    kernel.assign(static_cast<size_t>(kernelSize * kernelSize), 0.0f);

    // 누적해야된까 sum 변수
    double sum = 0.0;
    const double twoSigma2 = 2.0 * sigma * sigma;


    //중첩으로 현재 위치 찾고 가중치 

    for (int ky = -radius; ky <= radius; ++ky)
    {
        for (int kx = -radius; kx <= radius; ++kx)
        {
            const double v = std::exp(-(kx * kx + ky * ky) / twoSigma2);
            kernel[(ky + radius) * kernelSize + (kx + radius)] = static_cast<float>(v); // exp 가우시안 연산 통과한 값을 커널 필터에 넣고
            sum += v; // 누적
        }
    }

    for (float& w : kernel)
        w = static_cast<float>(w / sum); /// 밝기 변화는 그대로 가져가야되니까 나눠


}


// 가우시안 필터링 메소드 

int IpGaussian(const unsigned char* src, unsigned char* dst, int width, int height, int kernelSize, double sigma, const IpRoi* roi)
{
    if (!IpValidate(src, dst, width, height))
        return IP_ERR_NULL_PTR;

    kernelSize = IpClampKernel(kernelSize);
    const int radius = kernelSize / 2;
    std::vector<float> kernel;
    BuildGaussianKernel(kernelSize, sigma, kernel); // 위에 커널 객체

    int startX, startY, endX, endY;
    IpResolveRoi(roi, width, height, startX, startY, endX, endY);
    IpCopyImage(src, dst, width, height); // dst 복제

#pragma omp parallel for schedule(static) if((endY - startY) * (endX - startX) > 2048)
    for (int y = startY; y < endY; ++y)
    {
        for (int x = startX; x < endX; ++x)
        {
            float sum = 0.0f;

            for (int ky = -radius; ky <= radius; ++ky)
            {
                for (int kx = -radius; kx <= radius; ++kx)
                {
                    const float w = kernel[(ky + radius) * kernelSize + (kx + radius)];
                    sum += IpSample(src, x + kx, y + ky, width, height) * w;
                }
            }
            dst[y * width + x] = static_cast<unsigned char>(std::clamp(sum + 0.5f, 0.0f, 255.0f));
        }
    }
    return IP_OK;
}




int IpLaplacian(const unsigned char* src, unsigned char* dst, int width, int height, const IpRoi* roi)
{   


    if (!IpValidate(src, dst, width, height))
        return IP_ERR_NULL_PTR;



    // 라플라시안 커널 
    static const int k[3][3] = {
        { 0, -1, 0 },
        { -1, 4, -1 },
        { 0, -1, 0 }
    };

    int startX, startY, endX, endY;
    IpResolveRoi(roi, width, height, startX, startY, endX, endY);
    IpCopyImage(src, dst, width, height);

    // 이미지 순회하면서 
    for (int y = startY; y < endY; ++y)
    {
        for (int x = startX; x < endX; ++x)
        {
            int sum = 0;

            // 커널 순회 
            for (int ky = -1; ky <= 1; ++ky)
            {
                for (int kx = -1; kx <= 1; ++kx)
                    sum += static_cast<int>(IpSample(src, x + kx, y + ky, width, height)) * k[ky + 1][kx + 1];
                // 커널 convolution 연산 수행 하고 IpSample 메소드로 값 갱신
            }
            dst[y * width + x] = static_cast<unsigned char>(std::clamp(std::abs(sum), 0, 255));
            // 방향 상관 없이 알고 싶으니까 abs
        }
    }
    return IP_OK;
}

int IpSobel(const unsigned char* src, unsigned char* dst, int width, int height, const IpRoi* roi)
{
    if (!IpValidate(src, dst, width, height))
        return IP_ERR_NULL_PTR;




    // 소벨은 방향성 존재하는 커널 두개 x 커널 , y 커널 

    static const int gx[3][3] = {
        { -1, 0, 1 },
        { -2, 0, 2 },
        { -1, 0, 1 }
    };
    static const int gy[3][3] = {
        { -1, -2, -1 },
        { 0, 0, 0 },
        { 1, 2, 1 }
    };

    int startX, startY, endX, endY;
    IpResolveRoi(roi, width, height, startX, startY, endX, endY);
    IpCopyImage(src, dst, width, height);



    // 이미지 순회
    for (int y = startY; y < endY; ++y)
    {
        for (int x = startX; x < endX; ++x)
        {
            int sx = 0;
            int sy = 0;

           

            for (int ky = -1; ky <= 1; ++ky)
            {
                for (int kx = -1; kx <= 1; ++kx)
                {
                    const int v = IpSample(src, x + kx, y + ky, width, height);
                    sx += v * gx[ky + 1][kx + 1]; // 좌우 차이 계산 해서 세로선 나옴
                    sy += v * gy[ky + 1][kx + 1]; // 상하 차이 계산 가로선 
                }
            }

            // 마지막 sqrt 연산 해서 강도 구하기 
            const int mag = static_cast<int>(std::sqrt(static_cast<double>(sx * sx + sy * sy)));
            dst[y * width + x] = static_cast<unsigned char>(std::clamp(mag, 0, 255));
        }
    }
    return IP_OK;
}
