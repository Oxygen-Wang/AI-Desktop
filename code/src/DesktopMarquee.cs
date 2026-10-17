using System;
using System.Windows;

namespace QiDesk {
// Ctrl+左键框选时显示的桌面矩形；松开后把逻辑范围交给控制器选中分区。
public sealed class DesktopMarquee : IDisposable {
    readonly ManagerWindow manager;
    readonly Action<Rect> complete;
    SelectionPreview preview;
    int token;
    bool disposing;
    public DesktopMarquee(ManagerWindow owner,Action<Rect> action){manager=owner;complete=action;}
    public void Update(int session,Rect physical){
        if(disposing)return;
        if(physical.IsEmpty){if(session==token)DisposePreview();return;}
        if(preview==null||session!=token){DisposePreview();token=session;preview=new SelectionPreview();preview.Show();Shell.Attach(preview,Shell.DesktopHost());Shell.SetClickThrough(preview);}
        preview.SetBounds(DesktopDrag.Logical(physical,Shell.WindowScale(manager)));
    }
    public void Complete(int session,Rect physical){
        if(disposing)return;
        if(physical.IsEmpty){Update(session,Rect.Empty);return;}
        Update(session,physical);
        if(preview==null)return;
        Rect logical=DesktopDrag.Logical(physical,Shell.WindowScale(manager));
        SelectionPreview current=preview;
        DisposePreview();
        if(!disposing&&current!=null)complete(logical);
    }
    void DisposePreview(){var window=preview;preview=null;if(window!=null)window.Close();}
    public void Dispose(){disposing=true;DisposePreview();}
}
}
