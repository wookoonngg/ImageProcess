using System.Runtime.InteropServices;
using WpfImageProcessing.Models;

namespace WpfImageProcessing.Native
{
    internal static class NativeRoiHelper
    {
        /// <summary>
        /// ROI를 네이티브에 전달. null/invalid → IntPtr.Zero(전체 이미지).
        /// AllocHGlobal로 복사해 GCHandle 배열 핀의 불안정성을 피한다.
        /// </summary>
        public static IDisposable Pin(RoiData? roi, out IntPtr ptr)
        {
            if (roi == null || !roi.IsValid())
            {
                ptr = IntPtr.Zero;
                return EmptyDisposable.Instance;
            }

            var native = IpRoi.From(roi);
            ptr = Marshal.AllocHGlobal(Marshal.SizeOf<IpRoi>());
            Marshal.StructureToPtr(native, ptr, false);
            return new HGlobalDisposable(ptr);
        }

        private sealed class EmptyDisposable : IDisposable
        {
            public static readonly EmptyDisposable Instance = new();
            public void Dispose() { }
        }

        private sealed class HGlobalDisposable : IDisposable
        {
            private IntPtr _ptr;
            public HGlobalDisposable(IntPtr ptr) => _ptr = ptr;
            public void Dispose()
            {
                if (_ptr != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(_ptr);
                    _ptr = IntPtr.Zero;
                }
            }
        }
    }

    public static class IpStatus
    {
        public const int Ok = 0;
        public const int ErrNullPtr = -1;
        public const int ErrInvalidSize = -2;
        public const int ErrInvalidParam = -3;
        public const int ErrNotImplemented = -100;

        public static string ToMessage(int status) => status switch
        {
            Ok => "OK",
            ErrNullPtr => "null pointer",
            ErrInvalidSize => "invalid size",
            ErrInvalidParam => "invalid parameter",
            ErrNotImplemented => "C++ 연산 미구현 (스텁)",
            _ => $"native error ({status})"
        };
    }
}
