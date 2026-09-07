#include "ImageProcessingApi.h"

// WEEK 2 stub — implement Gaussian later
int IpGaussian(const unsigned char* src, unsigned char* dst, int width, int height, int kernelSize, double sigma, const IpRoi* roi)
{
    (void)src; (void)dst; (void)width; (void)height; (void)kernelSize; (void)sigma; (void)roi;
    return IP_ERR_NOT_IMPLEMENTED;
}

// WEEK 2 stub — implement Laplacian later
int IpLaplacian(const unsigned char* src, unsigned char* dst, int width, int height, const IpRoi* roi)
{
    (void)src; (void)dst; (void)width; (void)height; (void)roi;
    return IP_ERR_NOT_IMPLEMENTED;
}

// WEEK 2 stub — implement Sobel later
int IpSobel(const unsigned char* src, unsigned char* dst, int width, int height, const IpRoi* roi)
{
    (void)src; (void)dst; (void)width; (void)height; (void)roi;
    return IP_ERR_NOT_IMPLEMENTED;
}
