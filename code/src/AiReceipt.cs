using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace QiDesk {
// AI 通过 MCP 改动桌面后，主程序弹出一张回执：改了哪个分区、动了哪些文件，并能一键撤销。
public sealed class AiChange {
    public string Action {get;set;}
    public string Group {get;set;}
    public string Detail {get;set;}
    public string[] Files {get;set;}
    public DateTime When {get;set;}
}
public partial class Controller {
    public readonly List<AiChange> AiChanges=new List<AiChange>();
    DispatcherTimer aiReceiptTimer;
    public void RecordAiChange(string action,string group,string detail,string[] files){
        AiChanges.Add(new AiChange(){Action=action,Group=group,Detail=detail,Files=files??new string[0],When=DateTime.Now});
        if(AiChanges.Count>20)AiChanges.RemoveAt(0);
        ScheduleAiReceipt();
    }
    // 等 AI 这一轮连续改动停下来再弹，避免一次整理弹出很多张回执。
    public void ScheduleAiReceipt(){
        if(Manager==null)return;
        if(aiReceiptTimer==null){
            aiReceiptTimer=new DispatcherTimer(DispatcherPriority.Background,Manager.Dispatcher){Interval=TimeSpan.FromSeconds(1.5)};
            aiReceiptTimer.Tick+=delegate{aiReceiptTimer.Stop();if(Manager!=null)Manager.ShowAiReceipt("");};
        }
        aiReceiptTimer.Stop();aiReceiptTimer.Start();
    }
    public void ClearAiChanges(){AiChanges.Clear();}
}
public sealed class AiReceiptWindow : Window {
    readonly Controller controller;
    readonly TextBlock message;
    readonly StackPanel list;
    public AiReceiptWindow(Controller owner){
        controller=owner;Title="栖桌 · AI 修改回执";Width=520;Height=470;MinHeight=260;
        WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;
        Background=Brushes.Transparent;ShowInTaskbar=false;Topmost=true;Owner=owner.Manager;
        FontFamily=new FontFamily("Microsoft YaHei UI");
        var page=new StackPanel(){Margin=new Thickness(20)};
        page.Children.Add(Ui.Text("AI 修改回执",20,Ui.Ink));
        message=Ui.Text("",12,Ui.Muted);message.Margin=new Thickness(0,2,0,12);page.Children.Add(message);
        list=new StackPanel();
        page.Children.Add(new Border(){Child=new ScrollViewer(){Content=list,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,MaxHeight=300},Background=Ui.Surface,CornerRadius=new CornerRadius(12),Padding=new Thickness(12)});
        var actions=new WrapPanel(){Margin=new Thickness(0,14,0,0)};
        actions.Children.Add(Ui.Button("撤销最近一次修改",delegate{
            if(!controller.Store.Undo())return;
            controller.ClearAiChanges();
            if(controller.Running)controller.Rebuild();else controller.Refresh();
            Close();
        }));
        actions.Children.Add(Ui.Button("关闭",Close));
        page.Children.Add(actions);
        Content=new Border(){Child=page,Background=Ui.Base,CornerRadius=new CornerRadius(18),BorderBrush=Ui.Brush("#B6CBDC"),BorderThickness=new Thickness(1)};
    }
    public void Present(string note){
        message.Text=String.IsNullOrWhiteSpace(note)?"AI 刚刚修改了这些内容；可查看文件列表，或撤销最近一次修改。":note;
        list.Children.Clear();
        foreach(var change in controller.AiChanges.AsEnumerable().Reverse().Take(12)){
            var row=new StackPanel(){Margin=new Thickness(0,0,0,10)};
            var title=new TextBlock(){FontSize=13,Foreground=Ui.Ink,TextWrapping=TextWrapping.Wrap};
            title.Inlines.Add(new System.Windows.Documents.Run(change.Action));
            if(!String.IsNullOrEmpty(change.Group))title.Inlines.Add(new System.Windows.Documents.Run(" · "+change.Group){Foreground=Ui.Accent});
            row.Children.Add(title);
            if(!String.IsNullOrEmpty(change.Detail))row.Children.Add(new TextBlock(){Text=change.Detail,FontSize=11,Foreground=Ui.Muted,TextWrapping=TextWrapping.Wrap});
            if(change.Files!=null&&change.Files.Length>0){
                var files=new TextBlock(){FontSize=11,Foreground=Ui.Muted,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,2,0,0)};
                files.Text=String.Join("、",change.Files.Take(8))+(change.Files.Length>8?" 等 "+change.Files.Length+" 个文件":"");
                row.Children.Add(files);
            }
            list.Children.Add(row);
        }
        if(list.Children.Count==0)list.Children.Add(new TextBlock(){Text="还没有 AI 修改记录。",FontSize=12,Foreground=Ui.Muted});
        var area=SystemParameters.WorkArea;
        Left=area.Right-Width-24;Top=area.Bottom-Height-24;
        Show();Activate();
    }
}
public partial class ManagerWindow {
    AiReceiptWindow aiReceipt;
    public void ShowAiReceipt(string note){
        if(aiReceipt==null){aiReceipt=new AiReceiptWindow(controller);aiReceipt.Closed+=delegate{aiReceipt=null;};}
        aiReceipt.Present(note);
    }
    public bool AiReceiptVisible {get{return aiReceipt!=null&&aiReceipt.IsVisible;}}
}
}
