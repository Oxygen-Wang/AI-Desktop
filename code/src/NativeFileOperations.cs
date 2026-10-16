using System;
using System.IO;
using System.Text;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace QiDesk {
public static class NativeFileOperations {
    public static string ResolveShortcut(string file){IShellLink link=null;try{link=(IShellLink)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("00021401-0000-0000-C000-000000000046")));((IPersistFile)link).Load(file,0);var path=new StringBuilder(32768);link.GetPath(path,path.Capacity,IntPtr.Zero,0);return path.ToString();}catch{return "";}finally{if(link!=null)Marshal.ReleaseComObject(link);}}
    [ComImport,Guid("43826D1E-E718-42EE-BC55-A1E261C37BFE"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IShellItem {
        [PreserveSig]int BindToHandler(IntPtr context,ref Guid handler,ref Guid iid,out IntPtr result);
        [PreserveSig]int GetParent(out IShellItem parent);
        [PreserveSig]int GetDisplayName(uint flags,out IntPtr name);
        [PreserveSig]int GetAttributes(uint mask,out uint attributes);
        [PreserveSig]int Compare(IShellItem other,uint hint,out int order);
    }
    [ComImport,Guid("947AAB5F-0A5C-4C13-B4D6-4BF7836FC9F8"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IFileOperation {
        [PreserveSig]int Advise(IntPtr sink,out uint cookie);
        [PreserveSig]int Unadvise(uint cookie);
        [PreserveSig]int SetOperationFlags(uint flags);
        [PreserveSig]int SetProgressMessage([MarshalAs(UnmanagedType.LPWStr)]string message);
        [PreserveSig]int SetProgressDialog(IntPtr dialog);
        [PreserveSig]int SetProperties(IntPtr properties);
        [PreserveSig]int SetOwnerWindow(IntPtr owner);
        [PreserveSig]int ApplyPropertiesToItem(IShellItem item);
        [PreserveSig]int ApplyPropertiesToItems([MarshalAs(UnmanagedType.IUnknown)]object items);
        [PreserveSig]int RenameItem(IShellItem item,[MarshalAs(UnmanagedType.LPWStr)]string name,IntPtr sink);
        [PreserveSig]int RenameItems([MarshalAs(UnmanagedType.IUnknown)]object items,[MarshalAs(UnmanagedType.LPWStr)]string name);
        [PreserveSig]int MoveItem(IShellItem item,IShellItem target,[MarshalAs(UnmanagedType.LPWStr)]string name,IntPtr sink);
        [PreserveSig]int MoveItems([MarshalAs(UnmanagedType.IUnknown)]object items,IShellItem target);
        [PreserveSig]int CopyItem(IShellItem item,IShellItem target,[MarshalAs(UnmanagedType.LPWStr)]string name,IntPtr sink);
        [PreserveSig]int CopyItems([MarshalAs(UnmanagedType.IUnknown)]object items,IShellItem target);
        [PreserveSig]int DeleteItem(IShellItem item,IntPtr sink);
        [PreserveSig]int DeleteItems([MarshalAs(UnmanagedType.IUnknown)]object items);
        [PreserveSig]int NewItem(IShellItem target,uint attrs,[MarshalAs(UnmanagedType.LPWStr)]string name,[MarshalAs(UnmanagedType.LPWStr)]string template,IntPtr sink);
        [PreserveSig]int PerformOperations();
        [PreserveSig]int GetAnyOperationsAborted([MarshalAs(UnmanagedType.Bool)]out bool aborted);
    }
    [ComImport,Guid("000214F9-0000-0000-C000-000000000046"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IShellLink {
        void GetPath([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder path,int length,IntPtr data,uint flags);
        void GetIDList(out IntPtr pidl);void SetIDList(IntPtr pidl);
        void GetDescription([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder value,int length);void SetDescription([MarshalAs(UnmanagedType.LPWStr)]string value);
        void GetWorkingDirectory([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder value,int length);void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)]string value);
        void GetArguments([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder value,int length);void SetArguments([MarshalAs(UnmanagedType.LPWStr)]string value);
        void GetHotkey(out short key);void SetHotkey(short key);void GetShowCmd(out int cmd);void SetShowCmd(int cmd);
        void GetIconLocation([Out,MarshalAs(UnmanagedType.LPWStr)]StringBuilder value,int length,out int index);void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)]string value,int index);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)]string path,uint reserved);void Resolve(IntPtr hwnd,uint flags);void SetPath([MarshalAs(UnmanagedType.LPWStr)]string path);
    }
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)] static extern int SHCreateItemFromParsingName(string path,IntPtr context,ref Guid iid,out IShellItem item);
    public static bool Paste(IntPtr owner,string folder,object items,bool move){IShellItem destination=null;IFileOperation operation=null;try{Guid iid=typeof(IShellItem).GUID;Marshal.ThrowExceptionForHR(SHCreateItemFromParsingName(folder,IntPtr.Zero,ref iid,out destination));operation=(IFileOperation)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("3AD05575-8857-4850-9277-11B85BDB8E09")));Marshal.ThrowExceptionForHR(operation.SetOwnerWindow(owner));Marshal.ThrowExceptionForHR(operation.SetOperationFlags(0x40|0x20000000|0x200));Marshal.ThrowExceptionForHR(move?operation.MoveItems(items,destination):operation.CopyItems(items,destination));Marshal.ThrowExceptionForHR(operation.PerformOperations());bool aborted;Marshal.ThrowExceptionForHR(operation.GetAnyOperationsAborted(out aborted));return !aborted;}finally{if(operation!=null)Marshal.ReleaseComObject(operation);if(destination!=null)Marshal.ReleaseComObject(destination);}}
    public static bool Delete(IntPtr owner,string[] paths){
        if(paths==null||paths.Length==0)return false;
        var data=new System.Windows.Forms.DataObject();var files=new System.Collections.Specialized.StringCollection();files.AddRange(paths);data.SetFileDropList(files);
        object items=(IDataObject)data;IFileOperation operation=null;
        try{
            operation=(IFileOperation)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("3AD05575-8857-4850-9277-11B85BDB8E09")));
            Marshal.ThrowExceptionForHR(operation.SetOwnerWindow(owner));
            // 0x40 允许撤销（回收站）、0x200 不确认新建目录、0x4000 无法回收时再提示。
            Marshal.ThrowExceptionForHR(operation.SetOperationFlags(0x40|0x200|0x4000));
            Marshal.ThrowExceptionForHR(operation.DeleteItems(items));
            Marshal.ThrowExceptionForHR(operation.PerformOperations());
            bool aborted;Marshal.ThrowExceptionForHR(operation.GetAnyOperationsAborted(out aborted));return !aborted;
        }finally{if(operation!=null)Marshal.ReleaseComObject(operation);}
    }
    public static string Shortcut(string folder,string source){if(!Store.Exists(source))throw new FileNotFoundException("源文件暂不可访问。");string stem=Path.GetFileName(source.TrimEnd(Path.DirectorySeparatorChar));if(String.IsNullOrEmpty(stem))stem="文件夹";stem+=" - 快捷方式";string path=Path.Combine(folder,stem+".lnk");int i=2;while(Store.Exists(path))path=Path.Combine(folder,stem+" ("+i+++").lnk");IShellLink link=null;try{link=(IShellLink)Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("00021401-0000-0000-C000-000000000046")));link.SetPath(source);link.SetWorkingDirectory(Directory.Exists(source)?source:Path.GetDirectoryName(source));((IPersistFile)link).Save(path,true);return path;}finally{if(link!=null)Marshal.ReleaseComObject(link);}}
}
}
