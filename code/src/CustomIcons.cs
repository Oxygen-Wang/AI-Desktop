using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Interop;

namespace QiDesk {
public class CustomIcon {
    public string Source {get;set;}
    public int Index {get;set;}
}
// 用户为某个桌面项指定的显示图标。只改变栖桌内的显示：不写真实文件、不改 desktop.ini、
// 也不通知资源管理器，因此随时可以恢复，来源文件失效时自动回退系统图标。
public static class CustomIcons {
    public const int MaxEntries=2000;
    public const string Filter="图标与图片 (*.ico;*.exe;*.dll;*.icl;*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp)|*.ico;*.exe;*.dll;*.icl;*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp|所有文件 (*.*)|*.*";
    public static string LastError {get;private set;}
    static readonly Dictionary<string,ImageSource> cache=new Dictionary<string,ImageSource>(StringComparer.OrdinalIgnoreCase);
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)] static extern uint ExtractIconEx(string file,int index,IntPtr[] large,IntPtr[] small,uint count);
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr handle);

    public static void Validate(State s){
        var source=s.CustomIcons??new Dictionary<string,CustomIcon>(StringComparer.OrdinalIgnoreCase);
        var clean=new Dictionary<string,CustomIcon>(StringComparer.OrdinalIgnoreCase);
        foreach(var pair in source){
            if(pair.Key==null||pair.Value==null)continue;
            string path=pair.Key.Trim(),icon=(pair.Value.Source??"").Trim();
            // 只保留绝对路径：相对路径换一台机器或改工作目录就会指向别的文件。
            if(path.Length==0||!Path.IsPathRooted(path)||icon.Length==0||!Path.IsPathRooted(icon))continue;
            if(clean.Count>=MaxEntries)break;
            clean[path]=new CustomIcon(){Source=icon,Index=(int)Math.Max(0,Math.Min(1024,pair.Value.Index))};
        }
        s.CustomIcons=clean;
    }
    public static CustomIcon Get(State state,string path){
        if(state==null||state.CustomIcons==null||String.IsNullOrEmpty(path))return null;
        CustomIcon entry;return state.CustomIcons.TryGetValue(path,out entry)?entry:null;
    }
    public static bool Has(State state,string path){return Get(state,path)!=null;}
    public static void Set(State state,string path,string source,int index){
        if(state==null||String.IsNullOrEmpty(path)||String.IsNullOrEmpty(source))return;
        state.CustomIcons=state.CustomIcons??new Dictionary<string,CustomIcon>(StringComparer.OrdinalIgnoreCase);
        state.CustomIcons[path]=new CustomIcon(){Source=source,Index=Math.Max(0,index)};
        Forget(path);
    }
    public static bool Reset(State state,string path){
        if(state==null||state.CustomIcons==null||String.IsNullOrEmpty(path))return false;
        bool removed=state.CustomIcons.Remove(path);if(removed)Forget(path);return removed;
    }
    public static int Clear(State state){
        if(state==null||state.CustomIcons==null)return 0;
        int count=state.CustomIcons.Count;state.CustomIcons.Clear();cache.Clear();return count;
    }
    // 路径变化（重命名、移动父目录）后同步键，避免图标跟丢。
    public static void UpdateReferences(State state,string before,string after,bool directory){
        if(state==null||state.CustomIcons==null||String.IsNullOrEmpty(before))return;
        var moved=new Dictionary<string,CustomIcon>(StringComparer.OrdinalIgnoreCase);bool changed=false;
        foreach(var pair in state.CustomIcons){
            string path=ShellActions.UpdatePath(pair.Key,before,after,directory);
            if(!String.Equals(path,pair.Key,StringComparison.OrdinalIgnoreCase))changed=true;
            moved[path]=pair.Value;
        }
        if(!changed)return;
        state.CustomIcons=moved;cache.Clear();
    }
    static void Forget(string path){
        cache.Clear();
        if(!String.IsNullOrEmpty(path))Shell.ForgetIcon(path);
    }
    public static ImageSource Icon(State state,string path,double size){
        var entry=Get(state,path);if(entry==null)return null;
        string key=entry.Source.ToLowerInvariant()+"|"+entry.Index;
        ImageSource cached;if(cache.TryGetValue(key,out cached))return cached;
        var loaded=Load(entry,size);
        if(loaded==null){LastError="图标来源不可用，已回退系统图标："+entry.Source;return null;}
        try{loaded.Freeze();}catch{}
        if(cache.Count>400)cache.Clear();
        cache[key]=loaded;LastError="";return loaded;
    }
    static ImageSource Load(CustomIcon entry,double size){
        int pixels=(int)Math.Max(32,Math.Min(256,size<=0?64:size));
        try{
            if(String.IsNullOrEmpty(entry.Source)||!File.Exists(entry.Source))return null;
            // 图片与图标文件直接解码；程序、库文件取系统图标，序号大于 0 时按序号提取。
            if(entry.Index<=0||Thumbnails.IsPicture(entry.Source))return Thumbnails.Extract(entry.Source,pixels);
            var extracted=At(entry.Source,entry.Index);
            return extracted??Thumbnails.Extract(entry.Source,pixels);
        }catch(Exception ex){LastError=ex.Message;return null;}
    }
    public static int Count(string source){
        if(String.IsNullOrEmpty(source)||!File.Exists(source))return 0;
        try{uint total=ExtractIconEx(source,-1,null,null,0);return total==uint.MaxValue?0:(int)Math.Min(total,256);}catch{return 0;}
    }
    public static ImageSource At(string source,int index){
        if(String.IsNullOrEmpty(source))return null;
        var large=new IntPtr[1];
        if(ExtractIconEx(source,index,large,null,1)==0||large[0]==IntPtr.Zero)return null;
        try{var image=Imaging.CreateBitmapSourceFromHIcon(large[0],Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());image.Freeze();return image;}finally{DestroyIcon(large[0]);}
    }
}
// 图标来源选择窗口：图片直接用作图标，程序/库文件列出内含图标供挑选。
public sealed class CustomIconPicker : Window {
    public string Source {get;private set;}
    public int Index {get;private set;}
    readonly TextBox pathBox;
    readonly TextBlock hint;
    readonly WrapPanel grid;
    string source;int index;bool accepted;
    public CustomIconPicker(string title,string currentSource,int currentIndex,Window owner){
        Title=title;Width=560;Height=470;MinWidth=460;MinHeight=360;Owner=owner;WindowStartupLocation=WindowStartupLocation.CenterOwner;
        Background=Ui.Base;Foreground=Ui.Ink;FontFamily=new FontFamily("Microsoft YaHei UI");ShowInTaskbar=false;ResizeMode=ResizeMode.CanResize;
        var root=new StackPanel(){Margin=new Thickness(18)};
        root.Children.Add(Ui.Text("只更换栖桌内显示的图标，不会改写文件本身。",12,Ui.Muted));
        var row=new DockPanel(){Margin=new Thickness(0,4,0,8)};
        pathBox=Ui.Input(currentSource??"");pathBox.Margin=new Thickness(0,0,8,0);pathBox.TextChanged+=delegate{Choose(pathBox.Text.Trim());};
        var browse=Ui.Button("浏览…",delegate{Browse();});browse.Margin=new Thickness(0);
        DockPanel.SetDock(browse,Dock.Right);row.Children.Add(browse);row.Children.Add(pathBox);
        root.Children.Add(row);
        hint=Ui.Text("",11,Ui.Muted);root.Children.Add(hint);
        grid=new WrapPanel(){Margin=new Thickness(0,6,0,8)};
        root.Children.Add(new ScrollViewer(){Content=grid,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Height=210});
        var buttons=new StackPanel(){Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,8,0,0)};
        buttons.Children.Add(Ui.Button("确定",delegate{accepted=true;Source=source;Index=index;Close();}));
        buttons.Children.Add(Ui.Button("取消",delegate{Close();}));
        root.Children.Add(buttons);
        Content=root;
        Loaded+=delegate{grid.Children.Clear();Rebuild();};
        source=currentSource??"";index=Math.Max(0,currentIndex);
    }
    public bool Choose(){ShowDialog();return accepted;}
    void Choose(string value){
        if(String.Equals(value,source,StringComparison.OrdinalIgnoreCase))return;
        source=value;index=0;Rebuild();
    }
    void Browse(){
        var dialog=new Microsoft.Win32.OpenFileDialog(){Title="选择图标来源",Filter=CustomIcons.Filter,CheckFileExists=true};
        if(dialog.ShowDialog(this)!=true)return;
        pathBox.Text=dialog.FileName;source=dialog.FileName;index=0;Rebuild();
    }
    void Rebuild(){
        grid.Children.Clear();
        if(String.IsNullOrEmpty(source)||!File.Exists(source)){hint.Text="文件不存在，可点击“浏览…”选择 .ico、.exe、.dll、.icl 或图片。";return;}
        if(Thumbnails.IsPicture(source)){
            var picture=Thumbnails.Extract(source,96);
            if(picture==null){hint.Text="这个文件无法作为图标读取。";return;}
            grid.Children.Add(Cell(picture,0,"图片本身"));
            hint.Text="图片将按原图显示为图标。";
            return;
        }
        int total=CustomIcons.Count(source);
        if(total<=0){
            var fallback=Thumbnails.Extract(source,96);
            if(fallback==null){hint.Text="这个文件里没有可用图标。";return;}
            grid.Children.Add(Cell(fallback,0,"文件图标"));
            hint.Text="这个文件没有可枚举的图标组，使用它的默认图标。";
            return;
        }
        for(int i=0;i<total;i++){
            var icon=CustomIcons.At(source,i);if(icon==null)continue;
            grid.Children.Add(Cell(icon,i,"第 "+i+" 个"));
        }
        hint.Text="这个文件包含 "+total+" 个图标，点击选择一个。";
    }
    Border Cell(ImageSource image,int value,string tip){
        var border=new Border(){Width=52,Height=52,Margin=new Thickness(4),Padding=new Thickness(4),CornerRadius=new CornerRadius(8),Background=Ui.Surface,BorderBrush=value==index?Ui.Brush("#92AAE5"):Ui.Muted,BorderThickness=new Thickness(value==index?2:1),Cursor=System.Windows.Input.Cursors.Hand,ToolTip=tip};
        border.Child=new Image(){Source=image,Width=40,Height=40,Stretch=Stretch.Uniform};
        border.MouseLeftButtonUp+=delegate{index=value;foreach(var child in grid.Children){var other=child as Border;if(other!=null){other.BorderBrush=Ui.Muted;other.BorderThickness=new Thickness(1);}}border.BorderBrush=Ui.Brush("#92AAE5");border.BorderThickness=new Thickness(2);};
        return border;
    }
}
}
