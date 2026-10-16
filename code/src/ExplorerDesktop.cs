using System;
using System.Runtime.InteropServices;

namespace QiDesk {
// Restore Explorer's real desktop icon surface if an older QiDesk session hid it.
// Leaving this surface active preserves Windows selection, menus and OLE drops.
public static class ExplorerDesktop {
    [DllImport("user32.dll")] static extern bool IsWindowEnabled(IntPtr window);
    [DllImport("user32.dll")] static extern bool EnableWindow(IntPtr window,bool enabled);

    public static bool RestoreInput(){
        IntPtr desktop=Shell.DesktopHost();
        if(desktop==IntPtr.Zero)return false;
        // A disabled WorkerW blocks clicks and Explorer's desktop context menu
        // even when SysListView32 itself is visible and enabled.
        if(!IsWindowEnabled(desktop))EnableWindow(desktop,true);
        IntPtr view=Shell.DesktopView(desktop);
        if(view!=IntPtr.Zero&&!IsWindowEnabled(view))EnableWindow(view,true);
        IntPtr list=Shell.DesktopList(desktop);
        if(list!=IntPtr.Zero&&!IsWindowEnabled(list))EnableWindow(list,true);
        return IsWindowEnabled(desktop)&&list!=IntPtr.Zero&&IsWindowEnabled(list);
    }
    public static bool RestoreInteraction(){
        bool inputReady=RestoreInput();
        IntPtr desktop=Shell.DesktopHost();
        IntPtr list=Shell.DesktopList(desktop);
        if(list!=IntPtr.Zero&&!Shell.Visible(list))Shell.SetVisible(list,true);
        return inputReady&&list!=IntPtr.Zero;
    }
}
}
