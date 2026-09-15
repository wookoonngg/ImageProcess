#include "ImageProcessingApi.h"
#include "Common.hpp"
#include <algorithm>

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

    for (int y = startY; y < endY; ++y)
    {
        for (int x = startX; x < endX; ++x)
        {
            const int index = y * width + x;
            dst[index] = (src[index] >= thresholdValue) ? 255 : 0; 
        }
    }
    return IP_OK;
}
