using System.Runtime.InteropServices;
using WpfImageProcessing.Models;

namespace WpfImageProcessing.Native
{
    internal static class NativeRoiHelper
    {
        /// <summary>
        /// ROI를 네이티브에 전달. null이면 IntPtr.Zero(전체 이미지).
        /// 호출 후 Dispose로 unpin.
        /// </summary>
        public static IDisposable Pin(RoiData? roi, out IntPtr ptr)
        {
            if (roi == null || !roi.IsValid())
            {
                ptr = IntPtr.Zero;
                return EmptyDisposable.Instance;
            }

            // 단일 요소 배열로 pin → blittable struct 포인터 안정적으로 전달
            var boxed = new[] { IpRoi.From(roi) };
            var handle = GCHandle.Alloc(boxed, GCHandleType.Pinned);
            ptr = handle.AddrOfPinnedObject();
            return new GcHandleDisposable(handle);
        }

        private sealed class EmptyDisposable : IDisposable
        {
            public static readonly EmptyDisposable Instance = new();
            public void Dispose() { }
        }

        private sealed class GcHandleDisposable : IDisposable
        {
            private GCHandle _handle;
            public GcHandleDisposable(GCHandle handle) => _handle = handle;
            public void Dispose()
            {
                if (_handle.IsAllocated)
                    _handle.Free();
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
