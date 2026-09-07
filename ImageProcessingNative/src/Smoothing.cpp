#include "ImageProcessingApi.h"
#include <algorithm>
#include <cmath>





inline unsigned char GetPixelSafe(const unsigned char* src, int x, int y, int width, int height, int stride) {


    x = std::max(0, std::min(x, width - 1));
    y = std::max(0, std::min(y, height - 1));
    return src[y * stride + x];



}






// WEEK 2 stub — implement box/mean smoothing later
int IpSmoothing(const unsigned char* src, unsigned char* dst, int width, int height, int kernelSize, const IpRoi* roi)
{
    (void)src; (void)dst; (void)width; (void)height; (void)kernelSize; (void)roi;
    return IP_ERR_NOT_IMPLEMENTED;

    int stride = (width + 3) & ~3;


    int startX = 0, startY = 0;
    int endX = width, endY = height;


    if (roi != nullptr) {
        startX = std::max(0, roi->X);
        startY = std::max(0, roi->Y);
        endX = std::min(width, roi->X + roi->Width);
        endY = std::min(height, roi->Y + roi->Height);




    }




    std::copy(src, src + (stride * height), dst);

    int radius = kernelSize / 2;


    const float kernel3x3[3][3] = {
        { 1.0f / 16.0f, 2.0f / 16.0f, 1.0f / 16.0f },
        { 2.0f / 16.0f, 4.0f / 16.0f, 2.0f / 16.0f },
        { 1.0f / 16.0f, 2.0f / 16.0f, 1.0f / 16.0f }
    };



    //Convolution 연산 

    for (int y = startY; y < endY; y++) {
        for (int x = startX; x < endX; x++) {
            float sum = 0.0f;


            for (int ky = -radius; ky <= radius; ky++) {
                for (int kx = -radius; kx <= radius; kx++) {

                    unsigned char val = GetPixelSafe(src, x + kx, y + ky, width, height, stride);

                    if (kernelSize == 3) {
                        sum += val * kernel3x3[ky + radius][kx + radius];
                    }

                    else {
                        sum += val * (1.0f / kernelSize * kernelSize);




                    }




                }
            }



            dst[y * stride + x] = static_cast<unsigned char>(std::max(0.0f, std::min(255.0f, sum)));



        }
    }






}
