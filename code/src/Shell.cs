using System;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Collections.Generic;
using System.Text;
using System.Reflection;

namespace QiDesk {
public static class Shell {
    static readonly uint ownProcessId=(uint)Process.GetCurrentProcess().Id;
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindow(string cls,string title);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent,IntPtr after,string cls,string title);
    delegate bool EnumProc(IntPtr h,IntPtr p);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc fn,IntPtr p);
    [DllImport("user32.dll",SetLastError=true)] static extern IntPtr SetParent(IntPtr child,IntPtr parent);
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] static extern IntPtr GetWindowLongPtr(IntPtr h,int index);
    [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW",SetLastError=true)] static extern IntPtr SetWindowLongPtr(IntPtr h,int index,IntPtr value);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int ht,uint flags);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr h);
    [DllImport("user32.dll")] static extern IntPtr SetFocus(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr h);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h,out NativeRect rect);
    [DllImport("user32.dll")] static extern IntPtr GetShellWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h,out uint process);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr h,StringBuilder name,int length);
    [DllImport("user32.dll")] static extern IntPtr GetWindow(IntPtr h,uint command);
    [DllImport("kernel32.dll")] static extern void SetLastError(uint error);
    [StructLayout(LayoutKind.Sequential)] struct NativeRect {public int Left,Top,Right,Bottom;}
    [DllImport("user32.dll")] public static extern bool RegisterHotKey(IntPtr h,int id,uint mods,uint key);
    [DllImport("user32.dll")] public static extern bool UnregisterHotKey(IntPtr h,int id);
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)] static extern IntPtr SHGetFileInfo(string path,uint attributes,out SHFILEINFO info,uint size,uint flags);
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct SHFILEINFO { public IntPtr hIcon;public int iIcon;public uint attributes;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)]public string displayName;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=80)]public string typeName; }
    static Dictionary<string,ImageSource> icons=new Dictionary<string,ImageSource>(StringComparer.OrdinalIgnoreCase);
    public static string ShortcutIconPath(string path){
        if(!String.Equals(Path.GetExtension(path),".lnk",StringComparison.OrdinalIgnoreCase)||!File.Exists(path))return path;
        object shell=null,link=null;try{
            var type=Type.GetTypeFromProgID("WScript.Shell");if(type==null)return path;shell=Activator.CreateInstance(type);
            link=shell.GetType().InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{path});
            string target=Convert.ToString(link.GetType().InvokeMember("TargetPath",BindingFlags.GetProperty,null,link,null));
            target=Environment.ExpandEnvironmentVariables(target??"");return File.Exists(target)||Directory.Exists(target)?target:path;
        }catch{return path;}finally{if(link!=null&&Marshal.IsComObject(link))Marshal.ReleaseComObject(link);if(shell!=null&&Marshal.IsComObject(shell))Marshal.ReleaseComObject(shell);}
    }
    public static ImageSource Icon(string path){ImageSource cached;if(icons.TryGetValue(path,out cached))return cached;string imagePath=ShortcutIconPath(path);SHFILEINFO info;SHGetFileInfo(imagePath,0,out info,(uint)Marshal.SizeOf(typeof(SHFILEINFO)),0x100|0x0);if(info.hIcon==IntPtr.Zero)return null;try{cached=Imaging.CreateBitmapSourceFromHIcon(info.hIcon,Int32Rect.Empty,BitmapSizeOptions.FromWidthAndHeight(40,40));cached.Freeze();if(icons.Count>2000)icons.Clear();icons[path]=cached;return cached;}finally{DestroyIcon(info.hIcon);}}
    // 快捷方式换了图标后丢掉缓存，下一次绘制才拿得到新图标。
    public static void ForgetIcon(string path){if(!String.IsNullOrEmpty(path))icons.Remove(path);}
    public static bool IsDesktopCandidate(string cls,uint process,uint shellProcess,bool visible,bool desktopView,int width,int height){return (cls=="Progman"||cls=="WorkerW")&&process==shellProcess&&shellProcess!=0&&visible&&desktopView&&width>=640&&height>=400;}
    public static IntPtr DesktopHost(){
        uint shellProcess;GetWindowThreadProcessId(GetShellWindow(),out shellProcess);
        IntPtr host=IntPtr.Zero;long largest=0;
        EnumWindows(delegate(IntPtr h,IntPtr p){
            var name=new StringBuilder(100);GetClassName(h,name,name.Capacity);uint process;GetWindowThreadProcessId(h,out process);NativeRect rect;if(!GetWindowRect(h,out rect))return true;
            IntPtr view=FindWindowEx(h,IntPtr.Zero,"SHELLDLL_DefView",null);bool list=view!=IntPtr.Zero&&FindWindowEx(view,IntPtr.Zero,"SysListView32",null)!=IntPtr.Zero;
            int width=rect.Right-rect.Left,height=rect.Bottom-rect.Top;
            if(IsDesktopCandidate(name.ToString(),process,shellProcess,IsWindowVisible(h),list,width,height)){
                long area=(long)width*height;if(area>largest){host=h;largest=area;}
            }return true;
        },IntPtr.Zero);return host;
    }
    public static bool Attach(Window window,IntPtr parent){
        if(parent==IntPtr.Zero||!IsWindow(parent))return false;
        IntPtr h=new WindowInteropHelper(window).Handle;
        // WPF layered windows retain top-level screen coordinates. Desktop ownership keeps
        // them above the desktop without child-window clipping or a cross-process DPI reset.
        long style=GetWindowLongPtr(h,-16).ToInt64();
        if((style&0x40000000L)!=0)return false;
        IntPtr oldOwner=GetWindow(h,4);
        SetLastError(0);SetWindowLongPtr(h,-8,parent);
        bool ok=GetWindow(h,4)==parent;
        if(!ok){SetWindowLongPtr(h,-8,oldOwner);return false;}
        long extended=GetWindowLongPtr(h,-20).ToInt64();SetWindowLongPtr(h,-20,new IntPtr((extended|0x80L|0x08000000L)&~0x40000L));
        SetWindowPos(h,new IntPtr(1),0,0,0,0,0x0013|0x0020);
        return true;
    }
    public static bool DesktopBindingValid(Window window,IntPtr host){IntPtr h=new WindowInteropHelper(window).Handle;return IsWindow(h)&&GetWindow(h,4)==host&&(GetWindowLongPtr(h,-16).ToInt64()&0x40000000L)==0;}
    public static void Float(Window window){
        IntPtr handle=new WindowInteropHelper(window).EnsureHandle();
        SetWindowLongPtr(handle,-8,IntPtr.Zero);
        if(GetWindow(handle,4)!=IntPtr.Zero)throw new InvalidOperationException("无法将分区从桌面层提起。");
        SetActivatable(window,true);
    }
    public static bool FloatingBindingValid(Window window){
        IntPtr handle=new WindowInteropHelper(window).Handle;
        return IsWindow(handle)&&GetWindow(handle,4)==IntPtr.Zero;
    }
    public static void Raise(Window window){IntPtr h=new WindowInteropHelper(window).Handle;if(h!=IntPtr.Zero)SetWindowPos(h,IntPtr.Zero,0,0,0,0,0x0013|0x0020);}
    // 取消置顶但保留窗口和桌面挂接：置顶一组分区前先降下其它选中窗口，最后提起的那个才真的在最上面。
    public static void Demote(Window window){IntPtr h=new WindowInteropHelper(window).Handle;if(h!=IntPtr.Zero)SetWindowPos(h,new IntPtr(-2),0,0,0,0,0x0013|0x0002|0x0020);}
    public static void SetClickThrough(Window window){IntPtr handle=new WindowInteropHelper(window).Handle;long style=GetWindowLongPtr(handle,-20).ToInt64();SetWindowLongPtr(handle,-20,new IntPtr(style|0x20L|0x08000000L|0x80L));}
    public static long ExtendedStyle(Window window){return GetWindowLongPtr(new WindowInteropHelper(window).Handle,-20).ToInt64();}
    // 桌面组件以 WS_EX_NOACTIVATE 挂接，无法接收键盘。编辑时就临时允许激活并置前，
    // 结束编辑后再恢复为不抢焦点的桌面挂接状态。
    public static void SetActivatable(Window window,bool activatable){IntPtr h=new WindowInteropHelper(window).Handle;if(h==IntPtr.Zero)return;long extended=GetWindowLongPtr(h,-20).ToInt64();SetWindowLongPtr(h,-20,new IntPtr(activatable?(extended&~0x08000000L):(extended|0x08000000L)));}
    public static void BeginInlineInput(Window window){IntPtr h=new WindowInteropHelper(window).Handle;if(h==IntPtr.Zero)return;SetActivatable(window,true);try{window.Activate();}catch{}SetForegroundWindow(h);SetFocus(h);}
    public static void EndInlineInput(Window window){IntPtr h=new WindowInteropHelper(window).Handle;if(h==IntPtr.Zero)return;SetActivatable(window,false);SetWindowPos(h,new IntPtr(1),0,0,0,0,0x0013);}
    public static bool ClickThrough(Window window){return (GetWindowLongPtr(new WindowInteropHelper(window).Handle,-20).ToInt64()&0x20L)!=0;}
    public static bool PassiveDesktopWindow(IntPtr window,IntPtr host){return DesktopBindingValidHandle(window,host)&&(GetWindowLongPtr(window,-20).ToInt64()&0x20L)!=0;}
    public static bool DesktopBindingValidHandle(IntPtr window,IntPtr host){return host!=IntPtr.Zero&&IsWindow(window)&&GetWindow(window,4)==host&&ProcessId(window)==ownProcessId;}
    public static Rect Bounds(Window window){NativeRect r;return GetWindowRect(new WindowInteropHelper(window).Handle,out r)?new Rect(r.Left,r.Top,r.Right-r.Left,r.Bottom-r.Top):Rect.Empty;}
    public static double WindowScale(Window window){var source=PresentationSource.FromVisual(window);return source==null?1:source.CompositionTarget.TransformToDevice.M11;}
    public static bool NativeVisible(Window window){return IsWindowVisible(new WindowInteropHelper(window).Handle);}
    public static bool Visible(IntPtr window){return IsWindow(window)&&IsWindowVisible(window);}
    public static string ClassName(IntPtr window){var name=new StringBuilder(256);GetClassName(window,name,name.Capacity);return name.ToString();}
    public static uint ProcessId(IntPtr window){uint process;GetWindowThreadProcessId(window,out process);return process;}
    public static IntPtr DesktopView(IntPtr host){return FindWindowEx(host,IntPtr.Zero,"SHELLDLL_DefView",null);}
    public static IntPtr DesktopList(IntPtr host){IntPtr view=DesktopView(host);return view==IntPtr.Zero?IntPtr.Zero:FindWindowEx(view,IntPtr.Zero,"SysListView32",null);}
    public static bool DesktopSurface(IntPtr window,IntPtr host){return host!=IntPtr.Zero&&window!=IntPtr.Zero&&(window==host||window==DesktopView(host)||window==DesktopList(host));}
    [DllImport("user32.dll")] static extern bool ShowWindowAsync(IntPtr window,int command);
    public static bool SetVisible(IntPtr window,bool visible){return IsWindow(window)&&ShowWindowAsync(window,visible?4:0);}
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern bool SetProp(IntPtr window,string name,IntPtr value);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern IntPtr GetProp(IntPtr window,string name);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern IntPtr RemoveProp(IntPtr window,string name);
    public static List<Rect> WorkingAreas(Window window){double scale=WindowScale(window);var result=new List<Rect>();foreach(var screen in System.Windows.Forms.Screen.AllScreens){var r=screen.WorkingArea;var area=new Rect(r.Left/scale,r.Top/scale,r.Width/scale,r.Height/scale);if(screen.Primary)result.Insert(0,area);else result.Add(area);}return result;}
    [DllImport("user32.dll")] static extern IntPtr GetParent(IntPtr h);
    public static void Open(string path){Open(path,true);}
    // interactive 为 false 时不弹窗，把失败原因交给调用方：MCP 需要把错误返回给 AI 而不是卡住一个管道线程。
    public static void Open(string path,bool interactive){try{Process.Start(new ProcessStartInfo(path){UseShellExecute=true});FileActivity.Record(path);}catch(Exception ex){if(interactive)MessageBox.Show(ex.Message,"打开失败");else throw;}}
    public static void Reveal(string path){Reveal(path,true);}
    public static void Reveal(string path,bool interactive){if(Directory.Exists(path))Open(path,interactive);else RevealInExplorer(path,interactive);}
    // interactive 为 false 时不弹窗：MCP 需要把失败原因返回给 AI，而不是卡住一个管道线程。
    static void RevealInExplorer(string path,bool interactive){
        try{Process.Start(new ProcessStartInfo("explorer.exe","/select,"+FileSearch.Quote(path)){UseShellExecute=true});}
        catch(Exception ex){if(interactive)MessageBox.Show(ex.Message);else throw;}
    }
}
}
