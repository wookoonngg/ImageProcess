#include "ImageProcessingApi.h"
#include <algorithm>



// WEEK 2 stub — implement binarization later
int IpThreshold(const unsigned char* src, unsigned char* dst, int width, int height, int thresholdValue, const IpRoi* roi)
{
    (void)src; (void)dst; (void)width; (void)height; (void)thresholdValue; (void)roi;
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


    for (int y = startY; y < endY; y++) {
        for (int x = startX; x < endX; x++) {
            int index = y * stride + x;



            dst[index] = (src[index] >= thresholdValue) ? 255 : 0;
        }
    }









}
