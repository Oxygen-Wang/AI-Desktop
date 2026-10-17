using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace QiDesk {
// 每个桌面分区都能拥有自己的归类规则：扩展名、文件名条件、文件/文件夹。
// 规则列表和忽略规则一样即时保存，区别只是它决定“进哪个分区”。
public partial class ManagerWindow {
    public void EditPartitionRules(Group target){
        if(target==null)return;
        if(!String.IsNullOrEmpty(target.Folder)){Ui.Info("文件夹映射直接显示真实文件夹内容；规则归类只作用于桌面分区。",this);return;}
        var dialog=Ui.Dialog("规则 · "+target.Name,600,700,this);
        var page=new StackPanel(){Margin=new Thickness(22)};
        page.Children.Add(Ui.Text("「"+target.Name+"」的归类规则",20,Ui.Ink));
        page.Children.Add(Ui.Text("和忽略规则一样即时保存。符合规则的桌面文件会进入这个分区；打开“自动归类”后新文件自动进入，也可以点“立即整理”手动执行。",12,Ui.Muted));
        var list=new StackPanel();
        Action refresh=null;
        refresh=delegate{
            list.Children.Clear();
            var rules=controller.Store.Data.Rules.Where(r=>r.GroupId==target.Id).ToArray();
            foreach(var rule in rules){
                var row=new Grid(){Margin=new Thickness(0,0,0,12)};
                row.ColumnDefinitions.Add(new ColumnDefinition(){Width=GridLength.Auto});
                row.ColumnDefinitions.Add(new ColumnDefinition());
                row.ColumnDefinitions.Add(new ColumnDefinition(){Width=GridLength.Auto});
                var enabled=new CheckBox(){IsChecked=rule.Enabled,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,10,0),ToolTip="启用这条规则"};
                enabled.Click+=delegate{rule.Enabled=enabled.IsChecked==true;controller.SaveAndRefresh(false);};
                row.Children.Add(enabled);
                var text=new StackPanel();
                text.Children.Add(Ui.Text(rule.Name,14,Ui.Ink));
                text.Children.Add(Ui.Text(DescribeRule(rule),11,Ui.Muted));
                Grid.SetColumn(text,1);row.Children.Add(text);
                var remove=Ui.Button("移除",delegate{controller.Store.Checkpoint();controller.Store.Data.Rules.Remove(rule);controller.SaveAndRefresh();refresh();});
                Grid.SetColumn(remove,2);row.Children.Add(remove);
                list.Children.Add(row);
            }
            if(rules.Length==0)list.Children.Add(Ui.Text("还没有规则。可以按扩展名添加，例如 pdf、png、lnk。",12,Ui.Muted));
        };
        refresh();
        page.Children.Add(new Border(){Child=list,Background=Ui.Surface,CornerRadius=new CornerRadius(12),Padding=new Thickness(14),Margin=new Thickness(0,10,0,6)});
        string[] presets=PresetExtensions(target.Name);
        if(presets!=null)page.Children.Add(Ui.Button("补全「"+target.Name+"」常用扩展名规则",delegate{
            if(controller.Store.Data.Rules.Any(r=>r.GroupId==target.Id)){SetStatus("这个分区已有规则，未自动补全。");return;}
            controller.Store.Checkpoint();
            controller.Store.Data.Rules.Add(new Rule(){Name=target.Name+"类型",Extensions=String.Join(",",presets),NameMode="包含",Kind="文件",GroupId=target.Id,Enabled=true});
            controller.SaveAndRefresh(false);refresh();
        }));
        Section(page,"添加规则");
        var name=Ui.Input("");name.ToolTip="留空时按扩展名自动命名";
        page.Children.Add(Ui.Text("规则名称",12,Ui.Muted));page.Children.Add(name);
        var kind=Choice(page,"匹配对象",new[]{"全部","文件","文件夹"},0);
        var extensions=Ui.Input("pdf");
        page.Children.Add(Ui.Text("扩展名，以逗号分隔；留空匹配全部类型",12,Ui.Muted));page.Children.Add(extensions);
        var mode=Choice(page,"文件名条件",new[]{"包含","开头","结尾"},0);
        var words=Ui.Input("");
        page.Children.Add(Ui.Text("名称文字（留空匹配全部名称）",12,Ui.Muted));page.Children.Add(words);
        var actions=new WrapPanel();
        actions.Children.Add(Ui.Button("添加规则",delegate{
            controller.Store.Checkpoint();
            controller.Store.Data.Rules.Add(new Rule(){Name=String.IsNullOrWhiteSpace(name.Text)?RuleName(extensions.Text,words.Text):name.Text.Trim(),Extensions=extensions.Text.Trim(),Contains=words.Text,NameMode=Convert.ToString(mode.SelectedItem),Kind=Convert.ToString(kind.SelectedItem),GroupId=target.Id,Enabled=true});
            controller.SaveAndRefresh(false);name.Clear();refresh();
        }));
        actions.Children.Add(Ui.Button("立即整理",controller.ApplyRules));
        actions.Children.Add(Ui.Button("关闭",dialog.Close));
        page.Children.Add(actions);
        dialog.Content=new ScrollViewer(){Content=page,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
        dialog.ShowDialog();
    }
    public static string DescribeRule(Rule rule){
        if(rule==null)return "";
        string kind=rule.Kind=="文件"?"文件":rule.Kind=="文件夹"?"文件夹":"全部";
        string extensions=String.IsNullOrWhiteSpace(rule.Extensions)?"任意扩展名":rule.Extensions;
        string name=String.IsNullOrWhiteSpace(rule.Contains)?"任意名称":rule.NameMode+" "+rule.Contains;
        return kind+" · "+extensions+" · "+name;
    }
    static string RuleName(string extensions,string words){
        if(!String.IsNullOrWhiteSpace(words))return words.Trim();
        if(!String.IsNullOrWhiteSpace(extensions))return extensions.Split(new[]{',',';',' '},StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()+" 类型";
        return "新规则";
    }
    // 常见默认分区给一键补全用的扩展名；其它分区不提供预设。
    static string[] PresetExtensions(string name){
        if(name=="文档")return new[]{"doc","docx","pdf","txt","md","rtf","odt","wps","ppt","pptx","xls","xlsx","csv"};
        if(name=="图片")return new[]{"png","jpg","jpeg","gif","bmp","webp","tif","tiff","ico","heic","avif"};
        if(name=="应用")return new[]{"lnk","exe","url","bat","cmd","msi","appref-ms"};
        return null;
    }
}
}
