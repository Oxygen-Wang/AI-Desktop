using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace QiDesk {
// 只改快捷方式文件（.lnk）自己记录的图标位置，不碰目标程序，也不移动桌面文件。
// 图标来源可以是 .ico 文件，也可以是带图标的 .exe / .dll / .icl 加序号。
// 使用系统 IShellLink/IPersistFile：WScript.Shell 无法把图标位置清空（写空串会抛错）。
public static class ShortcutIcons {
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)] static extern void SHChangeNotify(uint eventId,uint flags,string path,IntPtr other);
    static readonly string[] sources={".ico",".exe",".dll",".icl"};
    public static string LastError="";
    public static bool IsShortcut(string path){
        return !String.IsNullOrEmpty(path)&&String.Equals(Path.GetExtension(path),".lnk",StringComparison.OrdinalIgnoreCase)&&File.Exists(path);
    }
    // 返回 "图标路径,序号"；没有自定义图标时返回空字符串。
    public static string Current(string path){
        if(!IsShortcut(path))return "";
        object link=null;
        try{
            link=Load(path);
            var buffer=new StringBuilder(1024);int index;int kind;
            ((IShellLinkW)link).GetIconLocation(buffer,buffer.Capacity,out index,out kind);
            string icon=buffer.ToString();
            return icon.Length==0?"":icon+","+index;
        }catch(Exception ex){LastError=ex.Message;return "";}
        finally{Release(link);}
    }
    public static void Apply(string path,string iconPath,int index){
        if(!IsShortcut(path))throw new ArgumentException("只有快捷方式（.lnk）可以更换图标，其它文件请用系统的方式处理。");
        if(String.IsNullOrWhiteSpace(iconPath))throw new ArgumentException("请选择一个图标来源文件。");
        iconPath=iconPath.Trim().Trim('"');
        if(!File.Exists(iconPath))throw new FileNotFoundException("找不到图标来源文件",iconPath);
        if(Array.IndexOf(sources,Path.GetExtension(iconPath).ToLowerInvariant())<0)throw new ArgumentException("图标来源必须是 .ico、.exe、.dll 或 .icl 文件。");
        if(index<0)throw new ArgumentOutOfRangeException("index","图标序号不能是负数。");
        Write(path,iconPath,index);
    }
    // 清空自定义图标，快捷方式回到目标程序自己的图标。
    public static void Reset(string path){
        if(!IsShortcut(path))throw new ArgumentException("只有快捷方式（.lnk）可以更换图标，其它文件请用系统的方式处理。");
        Write(path,"",0);
    }
    static void Write(string path,string iconPath,int index){
        object link=null;
        try{
            link=Load(path);
            ((IShellLinkW)link).SetIconLocation(iconPath,index);
            ((IPersistFile)link).Save(path,true);
        }finally{Release(link);}
        // 让资源管理器和其他窗口立刻重读这个快捷方式的图标，并丢掉本程序缓存的那一张。
        SHChangeNotify(0x00002000,0x0005,path,IntPtr.Zero);
        Shell.ForgetIcon(path);
    }
    static object Load(string path){
        object link=new ShellLink();
        ((IPersistFile)link).Load(path,2);
        return link;
    }
    static void Release(object link){
        if(link!=null&&Marshal.IsComObject(link))Marshal.ReleaseComObject(link);
    }
    [ComImport,Guid("00021401-0000-0000-C000-000000000046")]
    class ShellLink { }
    [ComImport,Guid("000214F9-0000-0000-C000-000000000046"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IShellLinkW {
        void GetPath([MarshalAs(UnmanagedType.LPWStr)] StringBuilder file,int maxPath,IntPtr findData,uint flags);
        void GetIDList(out IntPtr list);
        void SetIDList(IntPtr list);
        void GetDescription([MarshalAs(UnmanagedType.LPWStr)] StringBuilder name,int maxName);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);
        void GetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] StringBuilder directory,int maxPath);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string directory);
        void GetArguments([MarshalAs(UnmanagedType.LPWStr)] StringBuilder arguments,int maxPath);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string arguments);
        void GetHotkey(out short hotkey);
        void SetHotkey(short hotkey);
        void GetShowCmd(out int show);
        void SetShowCmd(int show);
        void GetIconLocation([MarshalAs(UnmanagedType.LPWStr)] StringBuilder icon,int maxPath,out int index,out int kind);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string icon,int index);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string relative,int reserved);
        void Resolve(IntPtr window,uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string file);
    }
    [ComImport,Guid("0000010B-0000-0000-C000-000000000046"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    interface IPersistFile {
        void GetClassID(out Guid id);
        [PreserveSig] int IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string file,uint mode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string file,[MarshalAs(UnmanagedType.Bool)] bool remember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string file);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string file);
    }
}
}
