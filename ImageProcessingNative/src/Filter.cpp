#include "ImageProcessingApi.h"
#include "Common.hpp"
#include <algorithm>
#include <cmath>
#include <vector>



// 가우시안 커널 필터를 일단 만들고 얘를 돌림
static void BuildGaussianKernel(int kernelSize, double sigma, std::vector<float>& kernel)
{   

    // 커널 사이즈 보정 홀수
    kernelSize = IpClampKernel(kernelSize);
    const int radius = kernelSize / 2; 

	// 시그마 변수 0 이하이면 커널 사이즈에 따라 적당히 설정

    if (sigma <= 0.0)
        sigma = std::max(0.5, kernelSize / 6.0);



    kernel.assign(static_cast<size_t>(kernelSize * kernelSize), 0.0f);

    // �����ؾߵȱ� sum ����
    double sum = 0.0;
    const double twoSigma2 = 2.0 * sigma * sigma;


    //��ø���� ���� ��ġ ã�� ����ġ 

    for (int ky = -radius; ky <= radius; ++ky)
    {
        for (int kx = -radius; kx <= radius; ++kx)
        {
            const double v = std::exp(-(kx * kx + ky * ky) / twoSigma2);
            kernel[(ky + radius) * kernelSize + (kx + radius)] = static_cast<float>(v); // exp ����þ� ���� ����� ���� Ŀ�� ���Ϳ� �ְ�
            sum += v; // ����
        }
    }

    for (float& w : kernel)
        w = static_cast<float>(w / sum); /// ��� ��ȭ�� �״�� �������ߵǴϱ� ����


}


// ����þ� ���͸� �޼ҵ� 

int IpGaussian(const unsigned char* src, unsigned char* dst, int width, int height, int kernelSize, double sigma, const IpRoi* roi)
{
    if (!IpValidate(src, dst, width, height))
        return IP_ERR_NULL_PTR;

    kernelSize = IpClampKernel(kernelSize);
    const int radius = kernelSize / 2;
    std::vector<float> kernel;
    BuildGaussianKernel(kernelSize, sigma, kernel); // ���� Ŀ�� ��ü

    int startX, startY, endX, endY;
    IpResolveRoi(roi, width, height, startX, startY, endX, endY);
    IpCopyImage(src, dst, width, height); // dst ����

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



    // ���ö�þ� Ŀ�� 
    static const int k[3][3] = {
        { 0, -1, 0 },
        { -1, 4, -1 },
        { 0, -1, 0 }
    };

    int startX, startY, endX, endY;
    IpResolveRoi(roi, width, height, startX, startY, endX, endY);
    IpCopyImage(src, dst, width, height);

    // �̹��� ��ȸ�ϸ鼭 
    for (int y = startY; y < endY; ++y)
    {
        for (int x = startX; x < endX; ++x)
        {
            int sum = 0;

            // Ŀ�� ��ȸ 
            for (int ky = -1; ky <= 1; ++ky)
            {
                for (int kx = -1; kx <= 1; ++kx)
                    sum += static_cast<int>(IpSample(src, x + kx, y + ky, width, height)) * k[ky + 1][kx + 1];
                // Ŀ�� convolution ���� ���� �ϰ� IpSample �޼ҵ�� �� ����
            }
            dst[y * width + x] = static_cast<unsigned char>(std::clamp(std::abs(sum), 0, 255));
            // ���� ��� ���� �˰� �����ϱ� abs
        }
    }
    return IP_OK;
}

int IpSobel(const unsigned char* src, unsigned char* dst, int width, int height, const IpRoi* roi)
{
    if (!IpValidate(src, dst, width, height))
        return IP_ERR_NULL_PTR;




    // �Һ��� ���⼺ �����ϴ� Ŀ�� �ΰ� x Ŀ�� , y Ŀ�� 

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



    // �̹��� ��ȸ
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
                    sx += v * gx[ky + 1][kx + 1]; // �¿� ���� ��� �ؼ� ���μ� ����
                    sy += v * gy[ky + 1][kx + 1]; // ���� ���� ��� ���μ� 
                }
            }

            // ������ sqrt ���� �ؼ� ���� ���ϱ� 
            const int mag = static_cast<int>(std::sqrt(static_cast<double>(sx * sx + sy * sy)));
            dst[y * width + x] = static_cast<unsigned char>(std::clamp(mag, 0, 255));
        }
    }
    return IP_OK;
}
