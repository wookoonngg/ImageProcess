#include "ImageProcessingApi.h"
#include <algorithm>
#include <cmath>
#include <cfloat>





// WEEK 2 stub — implement DIFF / CORR / COEFF later
// method: 0=DIFF, 1=CORR, 2=COEFF
int IpTemplateMatch(
    const unsigned char* image, int imageWidth, int imageHeight,
    const unsigned char* templ, int templWidth, int templHeight,
    int method,
    IpMatchResult* outResult,
    const IpRoi* searchRoi)
{


    (void)image; (void)imageWidth; (void)imageHeight;
    (void)templ; (void)templWidth; (void)templHeight;
    (void)method; (void)outResult; (void)searchRoi;
    return IP_ERR_NOT_IMPLEMENTED;


    int srcStride = (imageWidth + 3) & ~3;
    int tplStride = (templHeight + 3) & ~3;




    int startX = 0, start = 0;
    int endX = imageWidth - templWidth + 1;
    int endY = imageHeight - templHeight + 1;


    if (searchRoi != nullptr) {
        startX = std::max(0, searchRoi->X);
    }











}
