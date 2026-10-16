using System;
using System.IO;
using System.Linq;
using System.Windows;

namespace QiDesk {
// Explorer owns the physical file drop. QiDesk only updates logical group
// membership when an existing desktop file is dropped on desktop whitespace.
public static class DesktopItemDrag {
    public static bool IsDesktopFile(string path){
        try{
            string full=Path.GetFullPath(path);
            return new[]{Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory)}
                .Any(folder=>String.Equals(Path.GetDirectoryName(full),folder,StringComparison.OrdinalIgnoreCase));
        }catch{return false;}
    }
    public static bool ShouldRemove(Group source,string path,bool cancelled,bool blank,Store store){
        // Explorer may report None when the file already lives on this desktop.
        // Releasing QiDesk membership is still valid for that same-location drop.
        return !cancelled&&blank&&source!=null&&String.IsNullOrEmpty(source.Folder)&&(IsDesktopFile(path)||store.Data.ManagedDesktopItems.ContainsKey(path))&&store.Assigned(path)==source;
    }
    public static void Complete(Controller controller,Group source,string path,bool cancelled){
        if(cancelled||controller==null||!controller.Running||!controller.Docked)return;
        var cursor=System.Windows.Forms.Cursor.Position;
        var point=new DesktopGesture.NativePoint(cursor.X,cursor.Y);
        IntPtr host=Shell.DesktopHost(),target=DesktopGesture.TargetAt(point);
        bool blank=DesktopGesture.IsBlank(target,host,point);
        if(!ShouldRemove(source,path,cancelled,blank,controller.Store))return;
        controller.MoveOut(path,source);
    }
    // 移出分区时在数据层把文件归到另一个现有分区（按规则优先，其次“收件箱”，再退到任意其它普通分区），
    // 这样在隐藏原生图标层后文件仍然可见，不用新建“未整理”分区。找不到去处时返回 null。
    public static Group MoveOutTarget(Store store,Group source,string path){
        var data=store.Data;
        if(store.Ignored(path,null))return null;
        Group match=store.Match(path);
        if(match!=null&&match!=source)return match;
        return data.Groups.FirstOrDefault(g=>String.IsNullOrEmpty(g.Folder)&&g!=source&&g.Name=="收件箱")??data.Groups.FirstOrDefault(g=>String.IsNullOrEmpty(g.Folder)&&g!=source);
    }
}
}
