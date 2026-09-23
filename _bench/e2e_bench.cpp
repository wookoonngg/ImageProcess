#include <intrin.h>
#include <algorithm>
#include <chrono>
#include <cstdio>
#include <cstring>
#include <vector>
#include <omp.h>
using Clock = std::chrono::high_resolution_clock;
static double Ms(Clock::time_point a, Clock::time_point b){return std::chrono::duration<double,std::milli>(b-a).count();}

static void FullScalar(const unsigned char* s, unsigned char* d, int w, int h, unsigned char t){
  memcpy(d,s,(size_t)w*h);
  for(int y=0;y<h;++y){int row=y*w; for(int x=0;x<w;++x) d[row+x]=(s[row+x]>=t)?255:0;}
}
static void FullSSE(const unsigned char* s, unsigned char* d, int w, int h, unsigned char t){
  memcpy(d,s,(size_t)w*h);
  __m128i tv=_mm_set1_epi8((char)t);
  for(int y=0;y<h;++y){int x=0,row=y*w; for(;x+16<=w;x+=16){__m128i v=_mm_loadu_si128((const __m128i*)(s+row+x)); __m128i ge=_mm_cmpeq_epi8(_mm_min_epu8(v,tv),tv); _mm_storeu_si128((__m128i*)(d+row+x),ge);} for(;x<w;++x)d[row+x]=(s[row+x]>=t)?255:0;}
}
static void FullSSEOMP(const unsigned char* s, unsigned char* d, int w, int h, unsigned char t){
  memcpy(d,s,(size_t)w*h);
  __m128i tv=_mm_set1_epi8((char)t);
#pragma omp parallel for schedule(static)
  for(int y=0;y<h;++y){int x=0,row=y*w; for(;x+16<=w;x+=16){__m128i v=_mm_loadu_si128((const __m128i*)(s+row+x)); __m128i ge=_mm_cmpeq_epi8(_mm_min_epu8(v,tv),tv); _mm_storeu_si128((__m128i*)(d+row+x),ge);} for(;x<w;++x)d[row+x]=(s[row+x]>=t)?255:0;}
}
int main(){
  const int W=2048,H=2048,N=W*H,iters=40;
  std::vector<unsigned char> src(N),dst(N);
  for(int i=0;i<N;++i) src[i]=(unsigned char)(i*37);
  FullScalar(src.data(),dst.data(),W,H,128); FullSSE(src.data(),dst.data(),W,H,128); FullSSEOMP(src.data(),dst.data(),W,H,128);
  auto a=Clock::now(); for(int i=0;i<iters;++i) FullScalar(src.data(),dst.data(),W,H,128);
  auto b=Clock::now(); for(int i=0;i<iters;++i) FullSSE(src.data(),dst.data(),W,H,128);
  auto c=Clock::now(); for(int i=0;i<iters;++i) FullSSEOMP(src.data(),dst.data(),W,H,128);
  auto d=Clock::now();
  printf("E2E with memcpy (like IpThreshold), %dx%d, threads=%d\n",W,H,omp_get_max_threads());
  printf("Scalar+copy:  %7.3f ms  (baseline)\n", Ms(a,b)/iters);
  printf("SSE+copy:     %7.3f ms  (%.2fx)\n", Ms(b,c)/iters, (Ms(a,b)/iters)/(Ms(b,c)/iters));
  printf("SSE+OMP+copy: %7.3f ms  (%.2fx)\n", Ms(c,d)/iters, (Ms(a,b)/iters)/(Ms(c,d)/iters));
}
