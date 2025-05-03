using System.Runtime.InteropServices;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.Networking;

namespace PolyMod;

internal static class DarkMagic
{
    private delegate IntPtr InternalCreateAudioClipUsingDH_InjectedDelegate(IntPtr dh, IntPtr url, bool stream, bool compressed, AudioType audioType);
    private static readonly InternalCreateAudioClipUsingDH_InjectedDelegate InternalCreateAudioClipUsingDH_InjectedDelegateField 
        = IL2CPP.ResolveICall<InternalCreateAudioClipUsingDH_InjectedDelegate>(
            "UnityEngine.Networking.WebRequestWWW::InternalCreateAudioClipUsingDH_Injected"
        );

    internal static unsafe AudioClip? InternalCreateAudioClipUsingDH(UnityWebRequest uwr)
    {
        IntPtr dhPtr = uwr.downloadHandler == null ? IntPtr.Zero : uwr.downloadHandler.m_Ptr;
        string url = uwr.url;
        fixed (char* urlPtr = url)
        {
            ManagedSpanWrapper wrapper = new(urlPtr, url.Length);
            IntPtr result = InternalCreateAudioClipUsingDH_InjectedDelegateField(
                dhPtr,
                (nint)(&wrapper),
                false,
                false,
                AudioType.WAV
            );
            return result == IntPtr.Zero ? null : new AudioClip(result);
        }
    }
}

[StructLayout(LayoutKind.Sequential)]
internal unsafe struct ManagedSpanWrapper
{
    public void* pointer;
    public int length;

    public ManagedSpanWrapper(char* ptr, int len)
    {
        pointer = ptr;
        length = len;
    }
}
