using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace QiDesk {
public sealed class NativeMenuCommand {
    public string Label;public Action Action;public bool Checked;public bool Enabled=true;public List<NativeMenuCommand> Children;
    public NativeMenuCommand(string label,Action action){Label=label;Action=action;}
    public static NativeMenuCommand Submenu(string label,IEnumerable<NativeMenuCommand> children){return new NativeMenuCommand(label,null){Children=children.ToList()};}
}
public sealed class NativeMenus : IDisposable {
    [ComImport,Guid("000214E6-0000-0000-C000-000000000046"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IShellFolder {
        [PreserveSig]int ParseDisplayName(IntPtr hwnd,IntPtr context,[MarshalAs(UnmanagedType.LPWStr)]string name,ref uint eaten,out IntPtr pidl,ref uint attrs);
        [PreserveSig]int EnumObjects(IntPtr hwnd,uint flags,out IntPtr list);
        [PreserveSig]int BindToObject(IntPtr pidl,IntPtr context,ref Guid iid,[MarshalAs(UnmanagedType.Interface)]out object result);
        [PreserveSig]int BindToStorage(IntPtr pidl,IntPtr context,ref Guid iid,[MarshalAs(UnmanagedType.Interface)]out object result);
        [PreserveSig]int CompareIDs(IntPtr flags,IntPtr a,IntPtr b);
        [PreserveSig]int CreateViewObject(IntPtr hwnd,ref Guid iid,[MarshalAs(UnmanagedType.Interface)]out object result);
        [PreserveSig]int GetAttributesOf(uint count,[MarshalAs(UnmanagedType.LPArray,SizeParamIndex=0)]IntPtr[] pidls,ref uint attrs);
        [PreserveSig]int GetUIObjectOf(IntPtr hwnd,uint count,[MarshalAs(UnmanagedType.LPArray,SizeParamIndex=1)]IntPtr[] pidls,ref Guid iid,IntPtr reserved,[MarshalAs(UnmanagedType.Interface)]out object result);
        [PreserveSig]int GetDisplayNameOf(IntPtr pidl,uint flags,IntPtr name);
        [PreserveSig]int SetNameOf(IntPtr hwnd,IntPtr pidl,[MarshalAs(UnmanagedType.LPWStr)]string name,uint flags,out IntPtr result);
    }
    [ComImport,Guid("000214E4-0000-0000-C000-000000000046"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IContextMenu {
        [PreserveSig]int QueryContextMenu(IntPtr menu,uint index,uint first,uint last,uint flags);
        [PreserveSig]int InvokeCommand(ref InvokeInfo info);
        [PreserveSig]int GetCommandString(UIntPtr id,uint flags,IntPtr reserved,IntPtr name,uint length);
    }
    [ComImport,Guid("000214F4-0000-0000-C000-000000000046"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IContextMenu2 {
        [PreserveSig]int QueryContextMenu(IntPtr menu,uint index,uint first,uint last,uint flags);
        [PreserveSig]int InvokeCommand(ref InvokeInfo info);
        [PreserveSig]int GetCommandString(UIntPtr id,uint flags,IntPtr reserved,IntPtr name,uint length);
        [PreserveSig]int HandleMenuMsg(uint msg,IntPtr w,IntPtr l);
    }
    [ComImport,Guid("BCFCE0A0-EC17-11D0-8D10-00A0C90F2719"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IContextMenu3 {
        [PreserveSig]int QueryContextMenu(IntPtr menu,uint index,uint first,uint last,uint flags);
        [PreserveSig]int InvokeCommand(ref InvokeInfo info);
        [PreserveSig]int GetCommandString(UIntPtr id,uint flags,IntPtr reserved,IntPtr name,uint length);
        [PreserveSig]int HandleMenuMsg(uint msg,IntPtr w,IntPtr l);
        [PreserveSig]int HandleMenuMsg2(uint msg,IntPtr w,IntPtr l,out IntPtr result);
    }
    [DllImport("ole32.dll")] static extern int OleGetClipboard([MarshalAs(UnmanagedType.Interface)]out System.Runtime.InteropServices.ComTypes.IDataObject data);
    [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct InvokeInfo {
        public int Size;public uint Mask;public IntPtr Window,Verb,Parameters,Directory;public int Show;public uint HotKey;public IntPtr Icon,Title,VerbW,ParametersW,DirectoryW,TitleW;public NativePoint Point;
    }
    [StructLayout(LayoutKind.Sequential)] struct NativePoint {public int X,Y;}
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)] static extern int SHParseDisplayName(string name,IntPtr context,out IntPtr pidl,uint attrs,out uint resultAttrs);
    [DllImport("shell32.dll")] static extern int SHBindToParent(IntPtr pidl,ref Guid iid,[MarshalAs(UnmanagedType.Interface)]out IShellFolder folder,out IntPtr child);
    [DllImport("shell32.dll")] static extern int SHGetDesktopFolder(out IShellFolder desktop);
    [DllImport("user32.dll")] static extern IntPtr CreatePopupMenu();
    [DllImport("user32.dll")] static extern bool DestroyMenu(IntPtr menu);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern bool AppendMenu(IntPtr menu,uint flags,UIntPtr id,string label);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern bool InsertMenu(IntPtr menu,uint position,uint flags,UIntPtr id,string label);
    [DllImport("user32.dll")] static extern bool RemoveMenu(IntPtr menu,uint position,uint flags);
    [DllImport("user32.dll")] static extern uint TrackPopupMenuEx(IntPtr menu,uint flags,int x,int y,IntPtr owner,IntPtr options);
    [DllImport("user32.dll")] static extern int GetMenuItemCount(IntPtr menu);
    [DllImport("user32.dll")] static extern uint GetMenuItemID(IntPtr menu,int position);
    [DllImport("user32.dll")] static extern IntPtr GetSubMenu(IntPtr menu,int position);
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetMenuString(IntPtr menu,uint id,StringBuilder value,int max,uint flags);
    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr window);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr window,uint msg,IntPtr w,IntPtr l);
    readonly List<object> objects=new List<object>();IntPtr pidl,menu;IContextMenu context;IContextMenu2 context2;IContextMenu3 context3;
    readonly Dictionary<uint,Action> custom=new Dictionary<uint,Action>();uint nextCustom=0x8000;readonly IntPtr owner;
    public NativeMenus(IntPtr hwnd,string path,bool background){
        owner=hwnd;try{uint attrs;Marshal.ThrowExceptionForHR(SHParseDisplayName(path,IntPtr.Zero,out pidl,0,out attrs));Guid folderId=typeof(IShellFolder).GUID,contextId=typeof(IContextMenu).GUID;object obj;
            if(background){IShellFolder desktop;Marshal.ThrowExceptionForHR(SHGetDesktopFolder(out desktop));objects.Add(desktop);Marshal.ThrowExceptionForHR(desktop.BindToObject(pidl,IntPtr.Zero,ref folderId,out obj));objects.Add(obj);Marshal.ThrowExceptionForHR(((IShellFolder)obj).CreateViewObject(hwnd,ref contextId,out obj));}
            else{IShellFolder parent;IntPtr child;Marshal.ThrowExceptionForHR(SHBindToParent(pidl,ref folderId,out parent,out child));objects.Add(parent);Marshal.ThrowExceptionForHR(parent.GetUIObjectOf(hwnd,1,new[]{child},ref contextId,IntPtr.Zero,out obj));}
            objects.Add(obj);context=(IContextMenu)obj;context2=obj as IContextMenu2;context3=obj as IContextMenu3;menu=CreatePopupMenu();if(menu==IntPtr.Zero)throw new Exception("无法创建 Windows 菜单。");uint flags=4|16;if((Keyboard.Modifiers&ModifierKeys.Shift)!=0)flags|=0x100;Marshal.ThrowExceptionForHR(context.QueryContextMenu(menu,0,1,0x7fff,flags));
        }catch{Dispose();throw;}
    }
    string Verb(uint id){if(id<1||id>0x7fff)return "";IntPtr buffer=Marshal.AllocHGlobal(1024);try{for(int i=0;i<1024;i++)Marshal.WriteByte(buffer,i,0);return context.GetCommandString(new UIntPtr(id-1),4,IntPtr.Zero,buffer,512)>=0?Marshal.PtrToStringUni(buffer)??"":"";}finally{Marshal.FreeHGlobal(buffer);}}
    public void AddGroupCommands(IEnumerable<NativeMenuCommand> commands){AppendMenu(menu,0x800,UIntPtr.Zero,null);IntPtr child=Build(commands);AppendMenu(menu,0x10,new UIntPtr((ulong)child.ToInt64()),"栖桌");}
    IntPtr Build(IEnumerable<NativeMenuCommand> commands){IntPtr result=CreatePopupMenu();try{foreach(var command in commands){if(command.Children!=null){IntPtr child=Build(command.Children);if(!AppendMenu(result,0x10,new UIntPtr((ulong)child.ToInt64()),command.Label)){DestroyMenu(child);throw new Exception("无法添加菜单。");}}else{uint id=nextCustom++;custom[id]=command.Action;if(!AppendMenu(result,command.Checked?8u:0u,new UIntPtr(id),command.Label))throw new Exception("无法添加菜单。");}}return result;}catch{DestroyMenu(result);throw;}}
    public void AddBackgroundCommands(NativeMenuCommand configure,IEnumerable<NativeMenuCommand> views,IEnumerable<NativeMenuCommand> sorts,Action refresh){
        // A custom WPF view must apply view/sort/refresh to its own items.
        for(int i=GetMenuItemCount(menu)-1;i>=0;i--){string verb=Verb(GetMenuItemID(menu,i));if(verb=="refresh"||verb.StartsWith("view",StringComparison.OrdinalIgnoreCase)||verb.StartsWith("arrange",StringComparison.OrdinalIgnoreCase))RemoveMenu(menu,(uint)i,0x400);}
        Insert(menu,0,configure);InsertMenu(menu,1,0x400|0x800,UIntPtr.Zero,null);Insert(menu,2,NativeMenuCommand.Submenu("查看(&V)",views));Insert(menu,3,NativeMenuCommand.Submenu("排序方式(&O)",sorts));Insert(menu,4,new NativeMenuCommand("刷新(&E)",refresh));InsertMenu(menu,5,0x400|0x800,UIntPtr.Zero,null);
    }
    public bool HasVerb(string verb){return Inspect().Any(label=>label.EndsWith("|"+verb,StringComparison.OrdinalIgnoreCase));}
    public void AddPasteCommands(string folder,Action refresh){if(HasVerb("paste"))return;bool enabled=false;try{enabled=Clipboard.ContainsFileDropList();}catch{}uint position=(uint)GetMenuItemCount(menu);for(int i=0;i<GetMenuItemCount(menu);i++)if(Verb(GetMenuItemID(menu,i))=="properties"){position=(uint)i;break;}InsertMenu(menu,position++,0x400|0x800,UIntPtr.Zero,null);Insert(menu,position++,new NativeMenuCommand("粘贴(&P)",delegate{Paste(owner,folder,false);refresh();}){Enabled=enabled});Insert(menu,position++,new NativeMenuCommand("粘贴快捷方式(&S)",delegate{Paste(owner,folder,true);refresh();}){Enabled=enabled});InsertMenu(menu,position,0x400|0x800,UIntPtr.Zero,null);}
    public static void Paste(IntPtr hwnd,string folder,bool link){
        System.Runtime.InteropServices.ComTypes.IDataObject data=null;try{Marshal.ThrowExceptionForHR(OleGetClipboard(out data));uint preferred=1;try{var value=Clipboard.GetData("Preferred DropEffect") as MemoryStream;if(value!=null){byte[] bytes=value.ToArray();if(bytes.Length>=4)preferred=BitConverter.ToUInt32(bytes,0);}}catch{}if(link){foreach(string path in Clipboard.GetFileDropList())NativeFileOperations.Shortcut(folder,path);}else PasteData(hwnd,folder,data,preferred,false);}finally{if(data!=null&&Marshal.IsComObject(data))Marshal.ReleaseComObject(data);}
    }
    public static void PasteData(IntPtr hwnd,string folder,System.Runtime.InteropServices.ComTypes.IDataObject data,uint preferred,bool link){
        if(link)throw new ArgumentException("快捷方式使用文件路径创建。");NativeFileOperations.Paste(hwnd,folder,data,(preferred&2)!=0);
    }
    void Insert(IntPtr target,uint position,NativeMenuCommand command){if(command.Children!=null){IntPtr child=Build(command.Children);if(!InsertMenu(target,position,0x400|0x10,new UIntPtr((ulong)child.ToInt64()),command.Label)){DestroyMenu(child);throw new Exception("无法添加菜单。");}}else{uint id=nextCustom++;custom[id]=command.Action;if(!InsertMenu(target,position,0x400|(command.Enabled?0u:2u),new UIntPtr(id),command.Label))throw new Exception("无法添加菜单。");}}
    IntPtr Hook(IntPtr hwnd,int msg,IntPtr w,IntPtr l,ref bool handled){if(msg!=0x117&&msg!=0x2b&&msg!=0x2c&&msg!=0x120)return IntPtr.Zero;try{IntPtr result;if(context3!=null&&context3.HandleMenuMsg2((uint)msg,w,l,out result)>=0){handled=true;return result;}if(msg!=0x120&&context2!=null&&context2.HandleMenuMsg((uint)msg,w,l)>=0)handled=true;}catch{}return IntPtr.Zero;}
    public bool Show(Point screen,Func<string,bool> ownVerb){var source=HwndSource.FromHwnd(owner);if(source==null)throw new Exception("窗口尚未就绪。");source.AddHook(Hook);uint selected;try{SetForegroundWindow(owner);selected=TrackPopupMenuEx(menu,0x100|2,(int)Math.Round(screen.X),(int)Math.Round(screen.Y),owner,IntPtr.Zero);PostMessage(owner,0,IntPtr.Zero,IntPtr.Zero);}finally{source.RemoveHook(Hook);}if(selected==0)return false;Action action;if(custom.TryGetValue(selected,out action)){if(action!=null)action();return true;}string verb=Verb(selected);if(ownVerb!=null&&ownVerb(verb))return true;var info=new InvokeInfo(){Size=Marshal.SizeOf(typeof(InvokeInfo)),Mask=0x4000|0x20000000,Window=owner,Verb=new IntPtr(selected-1),VerbW=new IntPtr(selected-1),Show=1,Point=new NativePoint(){X=(int)screen.X,Y=(int)screen.Y}};if((Keyboard.Modifiers&ModifierKeys.Shift)!=0)info.Mask|=0x10000000;if((Keyboard.Modifiers&ModifierKeys.Control)!=0)info.Mask|=0x40000000;Marshal.ThrowExceptionForHR(context.InvokeCommand(ref info));return true;}
    public string[] Inspect(){var labels=new List<string>();Inspect(menu,labels);return labels.ToArray();}
    void Inspect(IntPtr current,List<string> labels){for(int i=0;i<GetMenuItemCount(current);i++){var text=new StringBuilder(512);GetMenuString(current,(uint)i,text,text.Capacity,0x400);string verb=Verb(GetMenuItemID(current,i));if(text.Length>0)labels.Add(text+"|"+verb);IntPtr sub=GetSubMenu(current,i);if(sub!=IntPtr.Zero)Inspect(sub,labels);}}
    public void Dispose(){if(menu!=IntPtr.Zero){DestroyMenu(menu);menu=IntPtr.Zero;}for(int i=objects.Count-1;i>=0;i--){var obj=objects[i];if(Marshal.IsComObject(obj))try{Marshal.ReleaseComObject(obj);}catch{}}objects.Clear();context=null;context2=null;context3=null;if(pidl!=IntPtr.Zero){Marshal.FreeCoTaskMem(pidl);pidl=IntPtr.Zero;}}
}
}
