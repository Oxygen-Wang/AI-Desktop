using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Interop;
using System.Runtime.InteropServices;

namespace QiDesk {
public static class DragTransfer {
    public static bool CanCopy(string[] paths,string destination){if(paths==null||paths.Length==0||paths.Length>1000||!Directory.Exists(destination))return false;return paths.All(p=>Store.Exists(p)&&(!Directory.Exists(p)||!WorkDetection.Under(destination,p))&&!String.Equals(IgnoreRule.Canonical(Path.GetDirectoryName(p)),IgnoreRule.Canonical(destination),StringComparison.OrdinalIgnoreCase));}
    public static DragDropEffects Effect(DragEventArgs e,string destination){var paths=Functions.DragPaths(e.Data);if(!CanCopy(paths,destination))return DragDropEffects.None;DragDropEffects effect=(e.KeyStates&DragDropKeyStates.ShiftKey)!=0&&(e.KeyStates&DragDropKeyStates.ControlKey)==0?DragDropEffects.Move:DragDropEffects.Copy;return (e.AllowedEffects&effect)!=0?effect:DragDropEffects.None;}
    public static bool Copy(Window owner,string[] paths,string destination,bool move){if(!CanCopy(paths,destination))throw new InvalidOperationException("请选择有效的目标文件夹；不能复制到自身或子目录，也不能放到文件上。");var data=new System.Windows.Forms.DataObject();var files=new System.Collections.Specialized.StringCollection();files.AddRange(paths);data.SetFileDropList(files);return NativeFileOperations.Paste(new WindowInteropHelper(owner).Handle,destination,(System.Runtime.InteropServices.ComTypes.IDataObject)data,move);}
    public static void Drop(Window owner,Controller c,DragEventArgs e,string destination,Action refresh){e.Handled=true;var effect=Effect(e,destination);if(effect==DragDropEffects.None)return;var paths=Functions.DragPaths(e.Data);if(paths==null)return;try{bool move=effect==DragDropEffects.Move;if(Copy(owner,paths,destination,move)){if(move){foreach(string p in paths)if(!Store.Exists(p)){string target=Path.Combine(destination,Path.GetFileName(p.TrimEnd('\\')));if(Store.Exists(target))ShellActions.UpdateReferences(c.Store.Data,p,target,Directory.Exists(target));}}c.SaveAndRefresh(false);refresh();}}catch(Exception ex){c.Manager.SetStatus("拖放未完成："+ex.Message);}}
}
}
