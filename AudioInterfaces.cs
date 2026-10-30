using System;
using System.Runtime.InteropServices;
[ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class MMEnumerator {}
[ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMEnumerator {
 [PreserveSig] int EnumAudioEndpoints(int flow, uint mask, out IntPtr collection);
 [PreserveSig] int GetDefaultAudioEndpoint(int flow, int role, out IMMDevice device);
 [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
 [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr callback);
 [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr callback);
}
[ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDevice {
 [PreserveSig] int Activate(ref Guid iid, uint context, IntPtr parameters, [MarshalAs(UnmanagedType.IUnknown)] out object instance);
 [PreserveSig] int OpenPropertyStore(uint mode, out IntPtr store);
 [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
 [PreserveSig] int GetState(out uint state);
}
[ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IEndpointVolume {
 [PreserveSig] int RegisterControlChangeNotify(IntPtr notify);
 [PreserveSig] int UnregisterControlChangeNotify(IntPtr notify);
 [PreserveSig] int GetChannelCount(out uint count);
 [PreserveSig] int SetMasterVolumeLevel(float level, ref Guid context);
 [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid context);
 [PreserveSig] int GetMasterVolumeLevel(out float level);
 [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
 [PreserveSig] int SetChannelVolumeLevel(uint channel, float level, ref Guid context);
 [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid context);
 [PreserveSig] int GetChannelVolumeLevel(uint channel, out float level);
 [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
 [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid context);
 [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
}
[ComImport, Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IAudioClientProbe {
 [PreserveSig] int Initialize(int mode, uint flags, long duration, long periodicity, IntPtr format, IntPtr session);
 [PreserveSig] int GetBufferSize(out uint size);
 [PreserveSig] int GetStreamLatency(out long latency);
 [PreserveSig] int GetCurrentPadding(out uint padding);
 [PreserveSig] int IsFormatSupported(int mode, IntPtr format, out IntPtr closest);
 [PreserveSig] int GetMixFormat(out IntPtr format);
}
