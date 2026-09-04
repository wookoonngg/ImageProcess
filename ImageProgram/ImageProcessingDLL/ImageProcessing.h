


extern "C" {
	__declspec(dlexport) void Threshold(unsigned char* pixelData, int width, int height, int threshold_value);

	__declspec(dllexport) void Gaussian(unsigned char* pixelData, int width, int height, unsigned char* ouputData);

}