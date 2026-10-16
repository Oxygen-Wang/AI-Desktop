using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace QiDesk {
public partial class ManagerWindow {
    Window tutorial;
    public void MaybeShowTutorial(){if(controller.Store.Data.TutorialVersion<1)ShowTutorial();}
    public void ShowTutorial(){if(tutorial!=null){tutorial.Activate();return;}tutorial=new TutorialWindow(controller);tutorial.Closed+=delegate{tutorial=null;};tutorial.Show();}
    void CloseTutorial(){if(tutorial!=null)tutorial.Close();}
    StackPanel TutorialPage(){var p=SettingsPage("使用教程","从左键框选开始，做一套属于自己的桌面。");p.Children.Add(Ui.Button("重新播放首次使用教程",ShowTutorial));foreach(var step in TutorialWindow.Steps){Section(p,step[0]);p.Children.Add(Ui.Text(step[1],14,Ui.Ink));}return p;}
}
public sealed class TutorialWindow : Window {
    readonly Controller controller;readonly StackPanel scene;readonly TextBlock title,description,progress;readonly Button next;int step;
    public static readonly string[][] Steps={
        new[]{"欢迎来到你的组件桌面","你可以自己挑选时钟、日历、待办、番茄钟等组件，组合出适合自己的桌面。在主页点“＋ 添加组件”开始。"},
        new[]{"左键长按，再框选","先启动桌面模式。在桌面空白处按住左键约 0.35 秒，再拖出一个区域，松开即可出现创建选项。按住 Shift 使用 Windows 原生框选；Esc 取消。"},
        new[]{"框选后，选择添加组件","松开左键后可创建桌面分组、文件夹映射，或在此添加组件。勾选喜欢的组件，它们会从你框选的位置排列。"},
        new[]{"组合、安装与分享","组件可以拖动、缩放和设置内容。把喜欢的组件勾选保存为组合；导出组合包发给朋友，对方在组件功能区导入安装。"},
        new[]{"Alt＋Space，打开工作区面板","Alt＋Space 打开或收起工作区面板，桌面小猫点击一下也能开合。面板顶部是文件夹面板标签：把文件夹拖到小猫身上或顶栏空白处就能新建，点标签切换、点 × 关闭。面板内可以直接移动、复制、重命名和删除真实文件。Ctrl＋F 按名称搜索；工作区搜索在小猫的右键菜单里。"}
    };
    public TutorialWindow(Controller owner){controller=owner;Title="栖桌 · 第一次使用";Width=650;Height=560;ResizeMode=ResizeMode.NoResize;WindowStartupLocation=WindowStartupLocation.CenterScreen;Background=Ui.Base;FontFamily=new FontFamily("Microsoft YaHei UI");Owner=owner.Manager;var body=new StackPanel(){Margin=new Thickness(30)};Content=body;progress=Ui.Text("",12,Ui.Accent);body.Children.Add(progress);title=Ui.Text("",25,Ui.Ink);body.Children.Add(title);scene=new StackPanel(){Margin=new Thickness(0,12,0,20)};body.Children.Add(new Border(){Child=scene,Height=220,CornerRadius=new CornerRadius(22),Background=Ui.Surface,Padding=new Thickness(24)});description=Ui.Text("",14,Ui.Ink);description.MinHeight=82;body.Children.Add(description);var buttons=new WrapPanel();body.Children.Add(buttons);buttons.Children.Add(Ui.Button("稍后再看",Close));buttons.Children.Add(Ui.Button("上一步",delegate{if(step>0){step--;RenderStep();}}));next=Ui.Button("下一步",delegate{if(step==Steps.Length-1){Close();owner.Manager.ShowComponentPicker();}else{step++;RenderStep();}});buttons.Children.Add(next);Closed+=delegate{controller.Store.Data.TutorialVersion=1;controller.Store.Save();};RenderStep();}
    public void RenderStep(){progress.Text="快速上手 · "+(step+1)+" / "+Steps.Length;title.Text=Steps[step][0];description.Text=Steps[step][1];next.Content=step==Steps.Length-1?"完成，选择我的组件":"下一步";scene.Children.Clear();
        if(step==0||step==3){var tiles=new WrapPanel();foreach(string name in new[]{"09:41 · 时钟","日历","□ 待办清单","25:00 · 专注"})tiles.Children.Add(new Border(){Child=Ui.Text(name,16,Ui.Ink),Width=230,Height=60,Background=Ui.Brush("#DDEBFA"),CornerRadius=new CornerRadius(14),Padding=new Thickness(14),Margin=new Thickness(0,0,12,12)});scene.Children.Add(tiles);}
        else if(step==1){scene.Children.Add(Ui.Text("① 空白处按住左键  →  ② 拖动框选  →  ③ 松开",15,Ui.Accent));scene.Children.Add(new Border(){Height=105,Width=350,BorderBrush=Ui.Accent,BorderThickness=new Thickness(2),Background=Ui.Brush("#DDEBFA"),CornerRadius=new CornerRadius(8),Child=new TextBlock(){Text="你想放组件的区域",HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center,Foreground=Ui.Accent}});}
        else if(step==2){foreach(string name in new[]{"在此创建桌面分组","在此创建文件夹映射","＋ 在此添加组件"})scene.Children.Add(new Border(){Child=Ui.Text(name,15,Ui.Ink),Background=name.Contains("组件")?Ui.Brush("#DDEBFA"):Ui.Base,CornerRadius=new CornerRadius(10),Padding=new Thickness(16,8,16,4),Margin=new Thickness(0,0,0,8)});}
        else{scene.Children.Add(Ui.Text("Alt  ＋  Space",28,Ui.Accent));scene.Children.Add(new Border(){Child=Ui.Text("▤  工作区面板：A文件夹 ×  B文件夹 ×  C文件夹 ×",18,Ui.Ink),Padding=new Thickness(20),Background=Ui.Brush("#DDEBFA"),CornerRadius=new CornerRadius(20)});scene.Children.Add(Ui.Text("拖动 = 移动    Ctrl＋拖动 = 复制    Ctrl＋F 搜索    Esc 收起",12,Ui.Muted));}
    }
}
}
