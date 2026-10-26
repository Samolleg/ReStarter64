#if RESTARTER64
using System;
using System.Runtime.InteropServices;

namespace EIStarter
{
    // DXGI inventory only. Vendor ID does not establish NGX/DLSS capability.
    internal static class NvidiaAdapter
    {
        [DllImport("dxgi.dll", ExactSpelling = true)]
        private static extern int CreateDXGIFactory(ref Guid iid, out Factory factory);

        [ComImport, Guid("7b7166ec-21c7-44ae-b21a-c9ae321ae369"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface Factory
        {
            [PreserveSig] int SetPrivateData(ref Guid name, uint size, IntPtr data);
            [PreserveSig] int SetPrivateDataInterface(ref Guid name, IntPtr data);
            [PreserveSig] int GetPrivateData(ref Guid name, ref uint size, IntPtr data);
            [PreserveSig] int GetParent(ref Guid iid, out IntPtr parent);
            [PreserveSig] int EnumAdapters(uint index, out Adapter adapter);
        }

        [ComImport, Guid("2411e7e1-12ac-4ccf-bd14-9798e8534dc0"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface Adapter
        {
            [PreserveSig] int SetPrivateData(ref Guid name, uint size, IntPtr data);
            [PreserveSig] int SetPrivateDataInterface(ref Guid name, IntPtr data);
            [PreserveSig] int GetPrivateData(ref Guid name, ref uint size, IntPtr data);
            [PreserveSig] int GetParent(ref Guid iid, out IntPtr parent);
            [PreserveSig] int EnumOutputs(uint index, out IntPtr output);
            [PreserveSig] int GetDesc(out AdapterDescription description);
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct AdapterDescription
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description;
            public uint VendorId, DeviceId, SubSysId, Revision;
            public UIntPtr DedicatedVideoMemory, DedicatedSystemMemory, SharedSystemMemory;
            public uint LuidLow;
            public int LuidHigh;
        }

        internal static bool IsPresent(out string reason)
        {
            reason = UiText.Language == "ru" ? "DLSS недоступен: адаптер NVIDIA не обнаружен."
                : "DLSS unavailable: no NVIDIA adapter detected.";
            Factory factory = null;
            try
            {
                Guid iid = typeof(Factory).GUID;
                Marshal.ThrowExceptionForHR(CreateDXGIFactory(ref iid, out factory));
                for (uint index = 0; ; index++)
                {
                    int result = factory.EnumAdapters(index, out Adapter adapter);
                    if (result == unchecked((int)0x887A0002)) return false; // DXGI_ERROR_NOT_FOUND
                    Marshal.ThrowExceptionForHR(result);
                    try
                    {
                        Marshal.ThrowExceptionForHR(adapter.GetDesc(out AdapterDescription description));
                        if (description.VendorId == 0x10DE) { reason = ""; return true; }
                    }
                    finally { if (adapter != null) Marshal.ReleaseComObject(adapter); }
                }
            }
            catch (Exception ex) when (ex is COMException || ex is DllNotFoundException || ex is EntryPointNotFoundException)
            {
                reason = UiText.Language == "ru" ? "DLSS недоступен: не удалось получить список адаптеров DXGI."
                    : "DLSS unavailable: DXGI adapter enumeration failed.";
                return false;
            }
            finally { if (factory != null) Marshal.ReleaseComObject(factory); }
        }
    }
}
#endif
