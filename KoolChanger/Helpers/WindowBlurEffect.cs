using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace KoolChanger.Helpers;

//ty f1den for this $$SUPERIOR$$ blur effect
internal class WindowBlurEffect
{
    private readonly uint _blurBackgroundColor = 0x990000;
    private uint _blurOpacity;

    //to call blur in our desired window
    internal WindowBlurEffect(Window window, AccentState accentState)
    {
        this.window = window;
        this.accentState = accentState;
        EnableBlur();
    }

    public double BlurOpacity
    {
        get => _blurOpacity;
        set
        {
            _blurOpacity = (uint)value;
            EnableBlur();
        }
    }

    private Window window { get; }
    private AccentState accentState { get; }

    [DllImport("user32.dll")]
    internal static extern int SetWindowCompositionAttribute(nint hwnd, ref WindowCompositionAttributeData data);

    internal void EnableBlur()
    {
        var windowHelper = new WindowInteropHelper(window);
        var accent = new AccentPolicy();


        accent.AccentState = accentState;
        accent.GradientColor = (_blurOpacity << 24) | (_blurBackgroundColor & 0xFFFFFF);


        var accentStructSize = Marshal.SizeOf(accent);

        var accentPtr = Marshal.AllocHGlobal(accentStructSize);
        Marshal.StructureToPtr(accent, accentPtr, false);

        var data = new WindowCompositionAttributeData();
        data.Attribute = WindowCompositionAttribute.WCA_ACCENT_POLICY;
        data.SizeOfData = accentStructSize;
        data.Data = accentPtr;

        SetWindowCompositionAttribute(windowHelper.Handle, ref data);

        Marshal.FreeHGlobal(accentPtr);
    }
}