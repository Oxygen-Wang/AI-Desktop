using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace QiDesk {
public sealed class DesktopCreation : IDisposable {
    readonly ManagerWindow manager;
    readonly Action<Rect,bool> create;
    readonly Action finished;
    SelectionPreview preview;
    int token;
    bool disposing;
    public DesktopCreation(ManagerWindow owner,Action<Rect,bool> action,Action reset){manager=owner;create=action;finished=reset;}
    public void Update(int session,Rect physical){
        if(disposing)return;
        if(physical.IsEmpty){if(session==token)DisposePreview();return;}
        if(preview==null||session!=token){DisposePreview();token=session;preview=new SelectionPreview();preview.SetBounds(DesktopDrag.Logical(physical,Shell.WindowScale(manager)));preview.Show();Shell.Attach(preview,Shell.DesktopHost());Shell.SetClickThrough(preview);}
        preview.SetBounds(DesktopDrag.Logical(physical,Shell.WindowScale(manager)));
    }
    public void Complete(int session,Rect physical){
        if(disposing)return;Update(session,physical);if(preview==null){finished();return;}
        Rect logical=DesktopDrag.Logical(physical,Shell.WindowScale(manager));
        SelectionPreview current=preview;
        preview.ShowChoice(logical,delegate(bool folder){if(disposing||preview!=current||token!=session)return;DisposePreview();finished();create(logical,folder);},delegate{if(disposing||preview!=current||token!=session)return;DisposePreview();finished();},delegate{if(disposing||preview!=current||token!=session)return;DisposePreview();finished();manager.ShowComponentPicker(logical);});
    }
    void DisposePreview(){var window=preview;preview=null;if(window!=null)window.Close();}
    public void Dispose(){disposing=true;DisposePreview();}
}
public sealed class SelectionPreview : Window {
    ContextMenu choice;
    public ContextMenu Choice {get{return choice;}}
    public SelectionPreview(){
        Title="栖桌 · 创建区域";WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;ShowInTaskbar=false;ShowActivated=false;Background=Brushes.Transparent;Width=10;Height=10;
        Content=new Border(){BorderBrush=Ui.Accent,BorderThickness=new Thickness(2),Background=new SolidColorBrush(Color.FromArgb(130,24,36,52))};
        Closed+=delegate{if(choice!=null)choice.IsOpen=false;};
    }
    public void SetBounds(Rect rect){Left=rect.X;Top=rect.Y;Width=Math.Max(1,rect.Width);Height=Math.Max(1,rect.Height);}
    public void ShowChoice(Rect rect,Action<bool> selected,Action cancelled,Action component=null){
        bool resolved=false;var menu=new ContextMenu(){Placement=PlacementMode.AbsolutePoint,HorizontalOffset=rect.Right,VerticalOffset=rect.Bottom,StaysOpen=false};choice=menu;
        menu.Items.Add(Ui.Menu("在此创建桌面分组",delegate{resolved=true;selected(false);}));
        menu.Items.Add(Ui.Menu("在此创建文件夹映射",delegate{resolved=true;selected(true);}));
        if(component!=null)menu.Items.Add(Ui.Menu("＋ 在此添加组件",delegate{resolved=true;component();}));
        menu.Items.Add(Ui.Menu("取消",delegate{resolved=true;cancelled();}));
        menu.Closed+=delegate{if(!resolved){resolved=true;cancelled();}};menu.IsOpen=true;
    }
}
}
