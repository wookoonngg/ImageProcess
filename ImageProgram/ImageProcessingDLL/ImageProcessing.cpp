void threshold(unsigned char* pixelData, int width, int height, int threshold_value) {
	for (int i = 0; i < width * height; ++i) {
		pixelData[i] = (pixelData[i] > threshold_value) ? 255 : 0;
	}
}