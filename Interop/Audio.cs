using System;
using System.Runtime.InteropServices;

// ---------- Windows Core Audio (COM) untuk mixer volume per aplikasi ----------
[ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class MMDeviceEnumeratorCom { }

[ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDeviceEnumerator
{
    void EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
    void GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
    void GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
    void RegisterEndpointNotificationCallback(IntPtr client);
    void UnregisterEndpointNotificationCallback(IntPtr client);
}

[ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDevice
{
    void Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
    void OpenPropertyStore(int access, out IntPtr props);
    void GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
    void GetState(out int state);
}

[ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IAudioSessionManager2
{
    void GetAudioSessionControl(ref Guid g, int flags, out IAudioSessionControl c);
    void GetSimpleAudioVolume(ref Guid g, int flags, out ISimpleAudioVolume v);
    void GetSessionEnumerator(out IAudioSessionEnumerator e);
    void RegisterSessionNotification(IntPtr n);
    void UnregisterSessionNotification(IntPtr n);
    void RegisterDuckNotification([MarshalAs(UnmanagedType.LPWStr)] string id, IntPtr n);
    void UnregisterDuckNotification(IntPtr n);
}

[ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IAudioSessionEnumerator
{
    void GetCount(out int n);
    void GetSession(int i, out IAudioSessionControl c);
}

[ComImport, Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IAudioSessionControl
{
    void GetState(out int state);
    void GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string n);
    void SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string n, ref Guid ctx);
    void GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string p);
    void SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string p, ref Guid ctx);
    void GetGroupingParam(out Guid g);
    void SetGroupingParam(ref Guid g, ref Guid ctx);
    void RegisterAudioSessionNotification(IntPtr n);
    void UnregisterAudioSessionNotification(IntPtr n);
}

[ComImport, Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IAudioSessionControl2
{
    void GetState(out int state);
    void GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string n);
    void SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string n, ref Guid ctx);
    void GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string p);
    void SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string p, ref Guid ctx);
    void GetGroupingParam(out Guid g);
    void SetGroupingParam(ref Guid g, ref Guid ctx);
    void RegisterAudioSessionNotification(IntPtr n);
    void UnregisterAudioSessionNotification(IntPtr n);
    void GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
    void GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
    void GetProcessId(out uint pid);
    [PreserveSig] int IsSystemSoundsSession();
    void SetDuckingPreference([MarshalAs(UnmanagedType.Bool)] bool opt);
}

[ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface ISimpleAudioVolume
{
    void SetMasterVolume(float level, ref Guid ctx);
    void GetMasterVolume(out float level);
    void SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid ctx);
    void GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
}

[ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IAudioEndpointVolume
{
    void RegisterControlChangeNotify(IntPtr n);
    void UnregisterControlChangeNotify(IntPtr n);
    void GetChannelCount(out uint c);
    void SetMasterVolumeLevel(float level, ref Guid ctx);
    void SetMasterVolumeLevelScalar(float level, ref Guid ctx);
    void GetMasterVolumeLevel(out float level);
    void GetMasterVolumeLevelScalar(out float level);
    void SetChannelVolumeLevel(uint ch, float level, ref Guid ctx);
    void SetChannelVolumeLevelScalar(uint ch, float level, ref Guid ctx);
    void GetChannelVolumeLevel(uint ch, out float level);
    void GetChannelVolumeLevelScalar(uint ch, out float level);
    void SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid ctx);
    void GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
}
