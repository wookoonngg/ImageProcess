#include "ImageProcessingApi.h"
#include <algorithm>
   





static inline unsigned char GetPixel(unsigned char* buf, int x, int y, int height, int width, int stride) {
    x = std::max(0, std::min(x, width - 1));
    y = std::max(0, std::min(y, height - 1));


	return buf[y * stride + x];


}


// WEEK 2 stub — implement dilation later
int IpDilation(const unsigned char* src, unsigned char* dst, int width, int height, int kernelSize, const IpRoi* roi)
{ // 팽창 => 흰색으로 채움 
    (void)src; (void)dst; (void)width; (void)height; (void)kernelSize; (void)roi;
    return IP_ERR_NOT_IMPLEMENTED;

    // 커널 내 max 값으로 대체해서 밝은 영역 확장 

   
	int stride = (width + 3) & ~3; // 4바이트 기준으로 정렬 8bit 크레이 스케일 

    // ROI 영역 설정 -> roi 가 NUll이 아니면 전체 이미지 처리

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

    for (int y = startY; y < endY; y++) {
        for (int x = startX; x < endX; x++) {
            unsigned char maxVal = 0;


            for (int ky = -radius; ky <= radius; ky++) {
                for (int kx = -radius; kx <= radius; kx++) {
					unsigned char val = GetPixel((unsigned char*)src, x + kx, y + ky, height, width, stride);

					maxVal = std::max(maxVal, val);
                }
            }




			dst[y * stride + x] = maxVal;

        }
    }



   

}







inline unsigned char GetPixelSafe(const unsigned char* src, int x, int y, int width, int height, int stride) {
	x = std::max(0, std::min(x, width - 1));
	y = std::max(0, std::min(y, height - 1));

    return src[y * stride + x];




}



// WEEK 2 stub — implement erosion later
int IpErosion(const unsigned char* src, unsigned char* dst, int width, int height, int kernelSize, const IpRoi* roi)
{
    (void)src; (void)dst; (void)width; (void)height; (void)kernelSize; (void)roi;
    return IP_ERR_NOT_IMPLEMENTED;



	int stride = (width + 3) & ~3; // 4바이트 기준으로 정렬 8bit 크레이 스케일

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


    for (int y = startY; y < endY; y++) {
        for (int x = startX; x < endX; x++) {
            unsigned char minVal = 255;

            for (int ky = -radius; ky <= radius; ky++) {
                for(int kx = -radius; kx <= radius; kx++) {
                    unsigned char val = GetPixelSafe(src, x + kx, y + ky, width, height, stride);
                    minVal = std::min(minVal, val);
				}
            }

			dst[y * stride + x] = minVal;




        }
    }







}
