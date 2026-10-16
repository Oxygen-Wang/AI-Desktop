using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace QiDesk {
// 公共函数库（渐进抽取）：把跨模块反复使用的基础操作集中到此。
// 约定：
//   · 只放无业务状态、可独立测试的小函数；
//   · 新增代码优先调用这里，旧代码按需逐步替换；
//   · 失败要能被调用方区分（返回 bool 或抛异常），不要静默吞掉。
public static class Functions {
    // ---- 文件属性 ----
    // 读取 Hidden 属性；路径无效或不可访问时返回 false，不抛异常。
    public static bool IsHidden(string path){
        try{return (File.GetAttributes(path)&FileAttributes.Hidden)!=0;}catch{return false;}
    }
    // 设置/清除 Hidden。仅当状态实际变化时返回 true。
    // 访问被拒绝等情况按原样抛出，让调用方决定回退方案。
    public static bool SetHidden(string path,bool hidden){
        FileAttributes attributes=File.GetAttributes(path);
        bool now=(attributes&FileAttributes.Hidden)!=0;
        if(now==hidden)return false;
        File.SetAttributes(path,hidden?attributes|FileAttributes.Hidden:attributes&~FileAttributes.Hidden);
        return true;
    }
    // ---- 原子写文件 ----
    // 先写临时文件，再替换目标并保留一份 .bak，避免留下半截配置。
    public static void WriteAtomic(string path,string text,string backup){
        string temp=path+".tmp";
        File.WriteAllText(temp,text);
        if(File.Exists(path))File.Replace(temp,path,backup);else File.Move(temp,path);
    }
    // ---- UI 线程调度 ----
    // 在指定 Dispatcher 上安全执行：已在线程上则同步执行，否则异步排队；已关闭则忽略。
    public static void OnUi(Dispatcher dispatcher,Action action){
        if(dispatcher==null||dispatcher.HasShutdownStarted||dispatcher.HasShutdownFinished)return;
        try{if(dispatcher.CheckAccess())action();else dispatcher.BeginInvoke(action);}catch{}
    }
    // ---- 拖拽 ----
    // 逻辑分区（桌面上的项目）拖拽时，来源和目标往往就是同一个桌面文件夹；只要让 Windows
    // 拿到文件数据（CF_HDROP），它就会去“复制/移动到自己”并弹出“源文件名和目标文件名相同”。
    // 因此逻辑拖拽只提供栖桌自己的格式，不含任何系统文件格式，Windows 与其它程序拿不到文件、
    // 不做任何文件操作；栖桌自己的落点通过 DragPaths 读取，归属与复制照常由栖桌处理。
    public const string LogicalFormat="QiDesk.LogicalPaths";
    public static DataObject LogicalDrag(string[] paths){
        var data=new DataObject();
        try{data.SetData(LogicalFormat,String.Join("\n",paths??new string[0]));}catch{}
        return data;
    }
    public static string[] DragPaths(System.Windows.IDataObject data){
        if(data==null)return null;
        try{
            if(data.GetDataPresent(DataFormats.FileDrop)){var files=data.GetData(DataFormats.FileDrop) as string[];if(files!=null&&files.Length>0)return files;}
            if(data.GetDataPresent(LogicalFormat)){var text=data.GetData(LogicalFormat) as string;if(!String.IsNullOrEmpty(text))return text.Split(new[]{'\n'},StringSplitOptions.RemoveEmptyEntries);}
        }catch{}
        return null;
    }
    public static string Timestamp(){return DateTime.Now.ToString("yyyyMMdd-HHmmss");}
}
}
