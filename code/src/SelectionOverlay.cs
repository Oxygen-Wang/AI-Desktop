using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace QiDesk {
public sealed class SelectionOverlay : Window {
    public event Action<Rect,bool> Selected;
    Point start;
    bool selecting;
    Border rectangle;
    Canvas canvas;
    public SelectionOverlay(Window owner){
        Owner=owner;WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=new SolidColorBrush(Color.FromArgb(30,0,0,0));ShowInTaskbar=false;Topmost=true;WindowStartupLocation=WindowStartupLocation.Manual;
        double scale=Shell.WindowScale(owner);var screen=System.Windows.Forms.SystemInformation.VirtualScreen;Left=screen.Left/scale;Top=screen.Top/scale;Width=screen.Width/scale;Height=screen.Height/scale;
        canvas=new Canvas();Content=canvas;var hint=new Border(){Background=Ui.Surface,CornerRadius=new CornerRadius(8),Padding=new Thickness(18,12,18,12),Child=Ui.Text("拖动鼠标框选范围，松开后选择分组或文件夹映射。Esc 取消。",14,Ui.Ink)};Canvas.SetLeft(hint,-Left+32);Canvas.SetTop(hint,32);canvas.Children.Add(hint);
        rectangle=new Border(){BorderBrush=Ui.Accent,BorderThickness=new Thickness(2),Background=new SolidColorBrush(Color.FromArgb(50,0,106,194)),Visibility=Visibility.Collapsed};canvas.Children.Add(rectangle);
        MouseLeftButtonDown+=delegate(object s,MouseButtonEventArgs e){start=e.GetPosition(this);selecting=true;CaptureMouse();rectangle.Visibility=Visibility.Visible;};
        MouseMove+=delegate(object s,MouseEventArgs e){if(!selecting)return;Rect r=Selection(start,e.GetPosition(this));Canvas.SetLeft(rectangle,r.X);Canvas.SetTop(rectangle,r.Y);rectangle.Width=r.Width;rectangle.Height=r.Height;};
        MouseLeftButtonUp+=delegate(object s,MouseButtonEventArgs e){if(!selecting)return;selecting=false;ReleaseMouseCapture();Rect chosen=Selection(start,e.GetPosition(this));if(chosen.Width<220||chosen.Height<100){rectangle.Visibility=Visibility.Collapsed;return;}chosen.Offset(Left,Top);var menu=new ContextMenu();menu.Items.Add(Ui.Menu("在此创建桌面分组",delegate{Finish(chosen,false);}));menu.Items.Add(Ui.Menu("在此创建文件夹映射",delegate{Finish(chosen,true);}));menu.Items.Add(Ui.Menu("取消",Close));menu.IsOpen=true;};
        KeyDown+=delegate(object s,KeyEventArgs e){if(e.Key==Key.Escape)Close();};
    }
    public static Rect Selection(Point first,Point second){return new Rect(new Point(Math.Min(first.X,second.X),Math.Min(first.Y,second.Y)),new Point(Math.Max(first.X,second.X),Math.Max(first.Y,second.Y)));}
    void Finish(Rect bounds,bool folder){Close();if(Selected!=null)Selected(bounds,folder);}
}
}
