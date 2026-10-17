using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace QiDesk {
public partial class ManagerWindow {
    void InheritedIgnoreRows(Panel body,Group group,Action rebuild){
        Section(body,"继承的全局规则");
        body.Children.Add(Ui.Text("在这里删除或修改仅影响此分区；恢复继承后继续使用全局设置。",12,Ui.Muted));
        foreach(var rule in controller.Store.Data.IgnoreRules.ToArray()){
            var current=rule;
            bool disabled=PartitionIgnores.Members(controller.Store.Data,group).Any(g=>g.DisabledGlobalIgnoreIds.Contains(rule.Id));
            var row=new DockPanel(){Margin=new Thickness(0,4,0,4)};
            var toggle=Ui.Button(disabled?"恢复继承":"仅此分区删除",delegate{SetInheritedIgnore(group,current.Id,!disabled);rebuild();});
            DockPanel.SetDock(toggle,Dock.Right);row.Children.Add(toggle);
            if(!disabled){
                var edit=Ui.Button("独立修改",delegate{
                    if(group.IgnoreRules.Count>=500){SetStatus("分区规则已达到上限。");return;}
                    controller.Store.Checkpoint();
                    var copy=Store.Json.Deserialize<IgnoreRule>(Store.Json.Serialize(current));copy.Id=Guid.NewGuid().ToString("N");
                    group.IgnoreRules.Add(copy);group.DisabledGlobalIgnoreIds.Add(current.Id);
                    SaveSettings(false);rebuild();
                });DockPanel.SetDock(edit,Dock.Right);row.Children.Add(edit);
            }
            var text=Ui.Text(current.Pattern+(disabled?" · 本分区已取消":""),13,disabled?Ui.Muted:Ui.Ink);text.VerticalAlignment=VerticalAlignment.Center;row.Children.Add(text);body.Children.Add(row);
        }
        foreach(var file in controller.Store.Data.IgnoreFiles.Where(f=>String.IsNullOrEmpty(f.ScopeId)).ToArray()){
            var current=file;
            bool disabled=PartitionIgnores.Members(controller.Store.Data,group).Any(g=>g.DisabledGlobalIgnoreIds.Contains(file.Id));
            var row=new WrapPanel(){Margin=new Thickness(0,6,0,6)};
            row.Children.Add(Ui.Text("导入规则 · "+System.IO.Path.GetFileName(file.Source)+(disabled?" · 已取消继承":""),13,Ui.Ink));
            row.Children.Add(Ui.Button(disabled?"恢复继承":"仅此分区删除",delegate{SetInheritedIgnore(group,current.Id,!disabled);rebuild();}));
            row.Children.Add(Ui.Button("复制到此分区编辑",delegate{
                if(controller.Store.Data.IgnoreFiles.Any(f=>f.ScopeId==group.Id&&String.Equals(f.Source,current.Source,StringComparison.OrdinalIgnoreCase))){SetStatus("此分区已有独立副本，请在下方直接修改。");return;}
                controller.Store.Checkpoint();
                var copy=Store.Json.Deserialize<IgnoreFileSet>(Store.Json.Serialize(current));
                copy.Id=Guid.NewGuid().ToString("N");copy.ScopeId=group.Id;copy.Automatic=false;copy.Revision="manual";
                IgnoreFiles.Import(controller.Store.Data,copy);
                if(!group.DisabledGlobalIgnoreIds.Contains(current.Id))group.DisabledGlobalIgnoreIds.Add(current.Id);
                SaveSettings(false);rebuild();
            }));body.Children.Add(row);
        }
    }
    void SetInheritedIgnore(Group group,string id,bool disabled){
        controller.Store.Checkpoint();
        foreach(var member in PartitionIgnores.Members(controller.Store.Data,group))member.DisabledGlobalIgnoreIds.RemoveAll(value=>value==id);
        if(disabled)group.DisabledGlobalIgnoreIds.Add(id);
        SaveSettings(false);
    }
}
}
