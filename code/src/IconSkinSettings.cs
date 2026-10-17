using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace QiDesk {
public partial class ManagerWindow {
    void AddAppIconSkins(Panel page,ComboBox target,ComboBox skin,Group[] groups){
        var content=new StackPanel();
        content.Children.Add(Ui.Text("只更换栖桌内的图标显示，支持按程序目标匹配；未匹配应用保留原图。",12,Ui.Muted));
        var cards=new WrapPanel();content.Children.Add(cards);
        foreach(string name in AppIconSkins.Choices.Where(AppIconSkins.IsPack)){
            string selected=name;var pack=AppIconSkins.Find(selected);
            var body=new StackPanel();body.Children.Add(Ui.Text(selected,14,Ui.Ink));
            var icons=new WrapPanel();
            foreach(string key in new[]{"chrome","edge","firefox","wechat","matlab","vscode","word","folder"}){
                var image=AppIconSkins.Image(pack,key);if(image==null)continue;
                icons.Children.Add(new Image(){Source=image,Width=28,Height=28,Margin=new Thickness(3),ToolTip=key});
            }
            body.Children.Add(icons);
            var apply=Ui.Button("应用这套图标",delegate{ApplyIconSkin(selected,target,skin,groups);});apply.IsEnabled=pack!=null;body.Children.Add(apply);
            body.Children.Add(Ui.Text(pack==null?AppIconSkins.LastError:"GPL-3.0 · "+pack.Entries.Count(e=>e.Executables.Length>0)+" 类应用映射",11,Ui.Muted));
            cards.Children.Add(new Border(){Child=body,Width=168,Padding=new Thickness(10),Margin=new Thickness(0,0,8,8),CornerRadius=new CornerRadius(8),Background=Ui.Surface});
        }
        content.Children.Add(Ui.Button("恢复系统图标",delegate{ApplyIconSkin("经典",target,skin,groups);}));
        page.Children.Add(new Expander(){Header="应用图标皮肤 · 预览与一键切换",Content=content,IsExpanded=false,Margin=new Thickness(0,4,0,8)});
    }
    void ApplyIconSkin(string style,ComboBox target,ComboBox skin,Group[] groups){
        controller.Store.Checkpoint();
        foreach(var group in target.SelectedIndex==0?groups:new[]{groups[target.SelectedIndex-1]})group.IconSkin=style;
        skin.SelectedItem=style;SaveSettings(true);
        SetStatus(style=="经典"?"已恢复系统图标，快捷方式原设置保持不变。":"已应用 "+style+"；未匹配应用保留原图，可撤销或恢复系统图标。");
    }
}
}
