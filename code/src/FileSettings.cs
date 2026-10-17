using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Data;

namespace QiDesk {
public partial class ManagerWindow {
    string ignoreScopeId="";
    PanelTab ignorePanelTab;
    public void IgnoreItem(string path,Group group){
        if(group!=null){group=controller.Store.Data.Groups.FirstOrDefault(g=>g.Id==group.Id);if(group==null)return;}
        var rules=group==null?controller.Store.Data.IgnoreRules:group.IgnoreRules;
        if(rules.Any(r=>r.Kind=="路径"&&String.Equals(IgnoreRule.Canonical(r.Pattern),IgnoreRule.Canonical(path),StringComparison.OrdinalIgnoreCase)))return;
        if(rules.Count>=500){Ui.Info("当前范围规则已达到上限。",this);return;}controller.Store.Checkpoint();rules.Add(new IgnoreRule(){Kind="路径",Pattern=Path.GetFullPath(path),IncludeChildren=Directory.Exists(path)});SaveSettings(false);SetStatus("已"+(group==null?"全局":"在此分区")+"忽略“"+Path.GetFileName(path)+"”；在忽略规则中可恢复。");
    }
    // 取消忽略：移除命中该路径的全局/分区忽略规则与“已移出”记录。
    // 由工作目录自动忽略或 .gitignore 等导入规则隐藏的项目需到对应页面调整，这里给出提示。
    public void UnignoreItem(string path){
        var data=controller.Store.Data;bool changed=false;
        changed|=data.IgnoreRules.RemoveAll(r=>r.Matches(path))>0;
        foreach(var group in data.Groups)changed|=group.IgnoreRules.RemoveAll(r=>r.Matches(path))>0;
        changed|=data.Excluded.RemoveAll(p=>String.Equals(p,path,StringComparison.OrdinalIgnoreCase))>0;
        if(changed){controller.SaveAndRefresh(false);SetStatus("已取消忽略："+Path.GetFileName(path));}
        else SetStatus("此项由工作目录或忽略文件规则隐藏，请在“忽略文件”或“AI 工作目录”页调整。");
    }
    // 面板快捷操作：新建桌面分区并切换到它；删除后回到“全部”。
    public Group CreateTabGroup(){
        string name=Ui.Ask("新建桌面分区","分区名称","新分区",this);if(String.IsNullOrWhiteSpace(name))return null;
        controller.Store.Checkpoint();var group=new Group(){Name=name,Page=controller.Store.Data.Page,X=60,Y=80};controller.Store.Data.Groups.Add(group);controller.SaveAndRefresh(false);
        if(workspaceSearch!=null)workspaceSearch.ActivateTab(group.Id);return group;
    }
    public void DeleteTabGroup(Group group){if(group==null)return;DeleteGroup(group);if(workspaceSearch!=null)workspaceSearch.ActivateTab("全部");}
    public void ShowDetails(Group group,string folder){ShowDetails(group,folder,null);}
    public void ShowDetails(Group group,string folder,string selected){new FileDetailsWindow(controller,group,folder,this,selected).Show();}
    public void ShowIgnoreRules(Group group,PanelTab tab=null){ignoreScopeId=group==null?"":group.Id;ignorePanelTab=tab;Show();WindowState=WindowState.Normal;Activate();ShowSettingsPage("忽略文件");}
    StackPanel IgnorePage(){
        var p=SettingsPage("忽略文件","隐藏匹配的文件和文件夹；全局规则用于所有分区，局部规则仅用于选中的分区。");
        var groups=controller.Store.Data.Groups.ToArray();var scope=Choice(p,"编辑的分区",new[]{"选择分区"}.Concat(groups.Select(g=>g.Name)),Array.FindIndex(groups,g=>g.Id==ignoreScopeId)+1);var body=new StackPanel();p.Children.Add(body);
        Action rebuild=null;rebuild=delegate{
            body.Children.Clear();Group group=scope.SelectedIndex<=0?null:groups[scope.SelectedIndex-1];
            IgnoreRuleRows(body,"全局规则",controller.Store.Data.IgnoreRules,rebuild);
            EditableIgnoreFileRows(body,null,rebuild);
            if(group!=null){
                IgnoreRuleRows(body,"「"+group.Name+"」的规则",group.IgnoreRules,rebuild);
                foreach(var folder in group.Folders.ToArray()){
                    List<IgnoreRule> local;if(!group.FolderIgnoreRules.TryGetValue(folder,out local)){local=new List<IgnoreRule>();group.FolderIgnoreRules[folder]=local;}
                    IgnoreRuleRows(body,"文件夹标签 · "+FilePresentation.FolderName(folder),local,rebuild);
                }
                EditableIgnoreFileRows(body,group,rebuild);
            }
            if(ignorePanelTab!=null&&ignorePanelTab.IsFolder){
                IgnoreRuleRows(body,"面板标签 · "+FilePresentation.FolderName(ignorePanelTab.Folder),ignorePanelTab.IgnoreRules,rebuild);
            }
            body.Children.Add(Ui.Text("忽略只影响显示，不移动或删除文件。导入规则以其项目目录为匹配根目录。",12,Ui.Muted));
        };scope.SelectionChanged+=delegate{rebuild();};rebuild();return p;
    }
    void IgnoreRuleRows(Panel body,string title,List<IgnoreRule> rules,Action rebuild){
        Section(body,title);
        var add=new DockPanel(){Margin=new Thickness(0,2,0,5)};
        var addButton=Ui.Button("＋ 添加",delegate{});DockPanel.SetDock(addButton,Dock.Right);add.Children.Add(addButton);
        var pattern=Ui.Input("");pattern.ToolTip="输入文件名或通配符，例如 *.tmp";add.Children.Add(pattern);
        addButton.Click+=delegate{try{var rule=new IgnoreRule(){Pattern=pattern.Text.Trim()};IgnoreRule.Validate(new List<IgnoreRule>{rule});if(rules.Count>=500)throw new InvalidDataException("当前范围规则已达到上限。");controller.Store.Checkpoint();rules.Add(rule);SaveSettings(false);rebuild();}catch(Exception ex){Ui.Info(ex.Message,this);}};
        body.Children.Add(add);
        foreach(var rule in rules.ToArray()){
            var current=rule;var row=new DockPanel(){Margin=new Thickness(0,1,0,3)};
            var remove=Ui.Button("×",delegate{controller.Store.Checkpoint();rules.Remove(current);SaveSettings(false);rebuild();});remove.ToolTip="恢复显示";DockPanel.SetDock(remove,Dock.Right);row.Children.Add(remove);
            var save=Ui.Button("保存",delegate{});DockPanel.SetDock(save,Dock.Right);row.Children.Add(save);
            var enabled=new CheckBox(){IsChecked=current.Enabled,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,8,0),ToolTip="启用规则"};DockPanel.SetDock(enabled,Dock.Left);row.Children.Add(enabled);
            enabled.Click+=delegate{controller.Store.Checkpoint();current.Enabled=enabled.IsChecked==true;SaveSettings(false);};
            var input=Ui.Input(current.Pattern);input.ToolTip=current.Kind=="路径"?"指定路径":"名称规则：支持 * 和 ?";row.Children.Add(input);
            save.Click+=delegate{try{var candidate=new IgnoreRule(){Id=current.Id,Kind=current.Kind,Pattern=input.Text.Trim(),Enabled=current.Enabled,IncludeChildren=current.IncludeChildren};IgnoreRule.Validate(new List<IgnoreRule>{candidate});controller.Store.Checkpoint();current.Pattern=candidate.Pattern;SaveSettings(false);}catch(Exception ex){Ui.Info(ex.Message,this);}};
            body.Children.Add(row);
        }
    }
    StackPanel WorkPage(){
        var p=SettingsPage("AI 工作目录","选择工作文件夹，检测项目标记与生成文件，并自动隐藏符合规则的项目。");var data=controller.Store.Data;
        Option(p,"连接 AI 客户端后自动识别项目目录",data.AutoDetectWorkspaces,delegate(bool value){data.AutoDetectWorkspaces=value;SaveSettings(false);});Option(p,"自动读取项目的 .gitignore / .cursorignore 等",data.AutoImportIgnoreFiles,delegate(bool value){data.AutoImportIgnoreFiles=value;IgnoreFiles.Sync(data);SaveSettings(false);});p.Children.Add(Ui.Text("支持 MCP Roots 的客户端会自动同步当前项目。其他客户端可调用连接当前工作目录工具，或在启动配置中指定工作目录。",12,Ui.Muted));
        Option(p,"自动隐藏 AI 生成文件名规则匹配的项目",data.WorkAutoIgnore,delegate(bool value){data.WorkAutoIgnore=value;SaveSettings(false);});
        Option(p,"同时隐藏临时文件、缓存和依赖目录",data.WorkIgnoreCaches,delegate(bool value){data.WorkIgnoreCaches=value;SaveSettings(false);});
        p.Children.Add(Ui.Text("生成文件规则：ChatGPT-、ChatGPT_、ChatGPT Image、Codex-、Codex_、Claude-、Claude_、generated-、generated_。依据名称判断；MCP 可登记其他名称的生成文件。",12,Ui.Muted));
        p.Children.Add(Ui.Button("＋ 添加工作目录",delegate{using(var picker=new System.Windows.Forms.FolderBrowserDialog()){picker.Description="选择 AI 工具生成文件的工作目录";if(picker.ShowDialog()!=System.Windows.Forms.DialogResult.OK)return;string path=Path.GetFullPath(picker.SelectedPath);if(data.WorkFolders.Count>=30){Ui.Info("最多支持 30 个工作目录。",this);return;}if(!data.WorkFolders.Any(r=>String.Equals(IgnoreRule.Canonical(r),IgnoreRule.Canonical(path),StringComparison.OrdinalIgnoreCase))){controller.Store.Checkpoint();WorkspaceConnections.Connect(data,path,"手动选择",false);SaveSettings(false);}ShowSettingsPage("AI 工作目录");}}));
        Section(p,"已选择的工作目录");foreach(string folder in data.WorkFolders.ToArray()){string root=folder;var row=new WrapPanel();row.Children.Add(Ui.Text(FilePresentation.FolderName(root)+(Directory.Exists(root)?"":" · 暂不可访问"),14,Ui.Ink));row.Children.Add(Ui.Button("打开",delegate{Shell.Open(root);}));row.Children.Add(Ui.Button("映射到桌面",delegate{var existing=data.Groups.FirstOrDefault(g=>String.Equals(IgnoreRule.Canonical(g.Folder),IgnoreRule.Canonical(root),StringComparison.OrdinalIgnoreCase));if(existing!=null){data.Page=existing.Page;SaveSettings(true);SetStatus("该目录已有映射。");return;}controller.CreateAt(new Rect(80,80,380,320),FilePresentation.FolderName(root),root);if(!controller.Running)controller.Start(true);}));row.Children.Add(Ui.Button("移除检测",delegate{controller.Store.Checkpoint();data.WorkFolders.Remove(root);if(!data.IgnoredWorkFolders.Contains(root,StringComparer.OrdinalIgnoreCase))data.IgnoredWorkFolders.Add(root);data.WorkSources.Remove(root);data.WorkConnectedAt.Remove(root);data.IgnoreFiles.RemoveAll(s=>s.Automatic&&s.Root==root);SaveSettings(false);ShowSettingsPage("AI 工作目录");}));p.Children.Add(row);if(data.WorkSources.ContainsKey(root))p.Children.Add(Ui.Text("来源："+data.WorkSources[root]+" · "+data.IgnoreFiles.Count(s=>s.Root==root)+" 个忽略文件",12,Ui.Muted));string[] markers=WorkDetection.Markers(root);if(markers.Length>0)p.Children.Add(Ui.Text("检测到项目标记："+String.Join("、",markers),12,Ui.Muted));}
        if(data.WorkFolders.Count==0)p.Children.Add(Ui.Text("连接 AI 客户端后项目会自动出现，也可手动添加目录。",13,Ui.Muted));
        if(data.IgnoredWorkFolders.Count>0){Section(p,"已停止自动关联的目录");foreach(string denied in data.IgnoredWorkFolders.ToArray()){string path=denied;var row=new WrapPanel();row.Children.Add(Ui.Text(FilePresentation.FolderName(path),13,Ui.Muted));row.Children.Add(Ui.Button("恢复关联",delegate{try{controller.Store.Checkpoint();WorkspaceConnections.Connect(data,path,"恢复检测",false);SaveSettings(false);ShowSettingsPage("AI 工作目录");}catch(Exception ex){Ui.Info(ex.Message,this);}}));p.Children.Add(row);}}
        Section(p,"检测结果");var results=new StackPanel();var scan=Ui.Button("检测当前工作目录",delegate{});scan.Click+=async delegate{scan.IsEnabled=false;results.Children.Clear();results.Children.Add(Ui.Text("正在检测…",13,Ui.Muted));try{var snapshot=Store.Json.Deserialize<State>(Store.Json.Serialize(data));var candidates=await Task.Run(()=>WorkDetection.Scan(snapshot));results.Children.Clear();foreach(var candidate in candidates){var item=candidate;var row=new StackPanel(){Margin=new Thickness(0,0,0,12)};row.Children.Add(Ui.Text(item.Name+" · "+item.Workspace,13,Ui.Ink));row.Children.Add(Ui.Text(item.Reason+(controller.Store.Ignored(item.Path,null)?" · 已自动或全局忽略":""),12,Ui.Muted));var actions=new WrapPanel();actions.Children.Add(Ui.Button("全局忽略此项目",delegate{IgnoreItem(item.Path,null);}));row.Children.Add(actions);results.Children.Add(row);}if(candidates.Count==0)results.Children.Add(Ui.Text("未发现未登记的候选项目。",13,Ui.Muted));results.Children.Add(Ui.Text("扫描最多 30 个目录、每目录 3000 项、子目录深度 2 层，跳过目录链接。自动隐藏规则不限这些扫描数量。",11,Ui.Muted));}catch(Exception ex){results.Children.Clear();results.Children.Add(Ui.Text("检测失败："+ex.Message,13,Ui.Muted));}finally{scan.IsEnabled=true;}};p.Children.Add(scan);p.Children.Add(results);return p;
    }
    StackPanel McpPage(){
        var p=SettingsPage("MCP 连接","让支持本地 MCP 的 AI 客户端读取分区、创建映射和维护忽略规则。");Option(p,"启用本机 MCP 连接",controller.Store.Data.McpEnabled,delegate(bool value){controller.Store.Data.McpEnabled=value;SaveSettings(false);controller.UpdateMcp();});
        p.Children.Add(Ui.Text("支持：分区与文件列表、工作目录、忽略规则、创建文件夹映射、登记生成文件。连接使用当前 Windows 用户的本机命名管道。",13,Ui.Muted));
        p.Children.Add(Ui.Text("支持 MCP Roots 的客户端会自动提供当前项目；其他客户端可调用 qidesk_connect_workspace 上报工作目录。连接后自动关联忽略文件，并显示在 AI 工作目录页面。",12,Ui.Muted));
        Section(p,"客户端配置");p.Children.Add(Ui.Button("复制 MCP 连接配置",delegate{SetStatus(Ui.CopyText(McpBridge.Configuration())?"MCP 配置已复制，可粘贴到支持 stdio MCP 的客户端。":"剪贴板暂被占用，可稍后重试或保存配置文件。");}));p.Children.Add(Ui.Button("保存 MCP 连接配置",delegate{string path=Path.Combine(controller.Store.Root,"qidesk-mcp.json");File.WriteAllText(path,McpBridge.Configuration());Shell.Reveal(path);}));p.Children.Add(Ui.Text("打开栖桌时连接可用，客户端配置会自动选择最新版服务。适用于支持本机 stdio MCP 的 AI 客户端。",12,Ui.Muted));return p;
    }
}
public class DetailRow {
    public string Path {get;set;} public bool ShowExtension {get;set;} public string Name {get{return FilePresentation.DisplayName(Path,ShowExtension);}}
    public ImageSource Icon {get{return Shell.Icon(Path);}} public string Size {get{return FilePresentation.Size(Path);}}
    public string Type {get{return FilePresentation.Type(Path);}} public string Modified {get{return FilePresentation.Modified(Path);}}
}
public sealed class FileDetailsWindow : Window {
    readonly Controller controller;readonly Group group;readonly string folder;PortalSession browser;readonly ListView list;readonly Image preview;readonly TextBlock previewName,previewDetail;readonly string initialSelection;
    public FileDetailsWindow(Controller c,Group g,string current,Window owner,string selected=null){
        controller=c;group=g;folder=current;if(!String.IsNullOrEmpty(g.Folder)){browser=new PortalSession(g.Folder);if(!String.IsNullOrEmpty(current))browser.Navigate(current);}else if(!String.IsNullOrEmpty(current)&&Directory.Exists(current))browser=new PortalSession(current);initialSelection=selected;Title=g.Name+" · 详情";Width=1120;Height=680;MinWidth=820;MinHeight=400;Owner=owner;WindowStartupLocation=WindowStartupLocation.CenterScreen;Background=Ui.Base;Foreground=Ui.Ink;FontFamily=new FontFamily("Microsoft YaHei UI");
        var root=new DockPanel(){Margin=new Thickness(24)};Content=root;var top=new StackPanel();DockPanel.SetDock(top,Dock.Top);root.Children.Add(top);top.Children.Add(Ui.Text(g.Name,24,Ui.Ink));top.Children.Add(Ui.Text("双击文件打开 · 双击文件夹进入 · Alt＋← 返回 · Ctrl＋滚轮调整大小",12,Ui.Muted));var actions=new WrapPanel();actions.Children.Add(Ui.Button("打开系统文件夹",delegate{Shell.Open(FilePresentation.OpenFolder(g,browser==null?"":browser.Current));}));actions.Children.Add(Ui.Button("‹ 返回",delegate{if(browser!=null&&!browser.Back()&&String.IsNullOrEmpty(group.Folder))browser=null;Refresh();}));actions.Children.Add(Ui.Button("↑ 上一级",delegate{if(browser!=null&&!browser.Parent()&&String.IsNullOrEmpty(group.Folder))browser=null;Refresh();}));actions.Children.Add(Ui.Button("刷新",Refresh));top.Children.Add(actions);var search=Ui.Input("");search.ToolTip="按名称筛选";top.Children.Add(search);
        list=new ListView(){Background=Ui.Surface,Foreground=Ui.Ink,BorderThickness=new Thickness(0),FontSize=13};var content=new Grid();content.ColumnDefinitions.Add(new ColumnDefinition());content.ColumnDefinitions.Add(new ColumnDefinition(){Width=new GridLength(260)});root.Children.Add(content);content.Children.Add(list);
        var previewBody=new StackPanel(){Margin=new Thickness(16,12,16,12)};previewBody.Children.Add(Ui.Text("预览",16,Ui.Ink));preview=new Image(){Width=220,Height=290,Stretch=Stretch.Uniform};previewBody.Children.Add(preview);previewName=Ui.Text("选择文件查看预览",14,Ui.Ink);previewName.Margin=new Thickness(0,14,0,8);previewBody.Children.Add(previewName);previewDetail=Ui.Text("",11,Ui.Muted);previewBody.Children.Add(previewDetail);var previewFrame=new Border(){Margin=new Thickness(14,0,0,0),CornerRadius=new CornerRadius(12),Background=Ui.Surface,Child=previewBody};Grid.SetColumn(previewFrame,1);content.Children.Add(previewFrame);list.SelectionChanged+=delegate{var row=list.SelectedItem as DetailRow;previewName.Text=row==null?"选择文件查看预览":row.Name;previewDetail.Text=row==null?"":FilePresentation.Detail(row.Path);Thumbnails.Bind(preview,row==null?"":row.Path,320);};
        var view=new GridView();list.View=view;
        var iconTemplate=new DataTemplate();var image=new FrameworkElementFactory(typeof(ThumbnailImage));image.SetBinding(ThumbnailImage.FilePathProperty,new Binding("Path"));image.SetValue(Image.WidthProperty,28.0);image.SetValue(Image.HeightProperty,28.0);iconTemplate.VisualTree=image;view.Columns.Add(new GridViewColumn(){Header="",Width=46,CellTemplate=iconTemplate});view.Columns.Add(new GridViewColumn(){Header="名称",Width=235,DisplayMemberBinding=new Binding("Name")});view.Columns.Add(new GridViewColumn(){Header="大小",Width=95,DisplayMemberBinding=new Binding("Size")});view.Columns.Add(new GridViewColumn(){Header="类型",Width=95,DisplayMemberBinding=new Binding("Type")});view.Columns.Add(new GridViewColumn(){Header="修改时间",Width=155,DisplayMemberBinding=new Binding("Modified")});
        search.TextChanged+=delegate{Refresh(search.Text);};list.MouseDoubleClick+=delegate(object s,MouseButtonEventArgs e){var row=ItemAt(e.OriginalSource as DependencyObject);if(row!=null){if(Directory.Exists(row.Path)&&controller.Store.Data.PortalNavigate){if(browser==null)browser=new PortalSession(row.Path);else browser.Navigate(row.Path);Refresh(search.Text);}else Shell.Open(row.Path);e.Handled=true;}};
        list.PreviewMouseRightButtonDown+=delegate(object s,MouseButtonEventArgs e){var row=ItemAt(e.OriginalSource as DependencyObject);if(row!=null)list.SelectedItem=row;};
        list.PreviewMouseWheel+=delegate(object s,MouseWheelEventArgs e){if((Keyboard.Modifiers&ModifierKeys.Control)==0)return;list.FontSize=Math.Max(11,Math.Min(24,list.FontSize+Math.Sign(e.Delta)));e.Handled=true;};
        list.ContextMenu=new ContextMenu();list.ContextMenuOpening+=delegate(object sender,ContextMenuEventArgs e){e.Handled=true;var row=e.CursorLeft<0?list.SelectedItem as DetailRow:ItemAt(e.OriginalSource as DependencyObject);Point point=e.CursorLeft<0?list.PointToScreen(new Point(16,16)):PointToScreen(Mouse.GetPosition(this));c.Manager.ShowFileMenu(this,row==null?FilePresentation.OpenFolder(g,browser==null?"":browser.Current):row.Path,g,point,row==null,delegate{Refresh(search.Text);});};
        bool scrollPending=false;list.AddHandler(ScrollViewer.ScrollChangedEvent,new ScrollChangedEventHandler(delegate{if(scrollPending)return;scrollPending=true;Dispatcher.BeginInvoke(new Action(delegate{scrollPending=false;Thumbnails.RequestVisible(list);}),System.Windows.Threading.DispatcherPriority.Background);}));

        PreviewKeyDown+=delegate(object s,KeyEventArgs e){Key key=e.Key==Key.System?e.SystemKey:e.Key;if((Keyboard.Modifiers&ModifierKeys.Alt)!=0&&(key==Key.Left||key==Key.Up)){if(browser!=null){bool done=key==Key.Left?browser.Back():browser.Parent();if(!done&&String.IsNullOrEmpty(group.Folder))browser=null;Refresh(search.Text);}e.Handled=true;}};list.AllowDrop=true;list.DragOver+=delegate(object s,DragEventArgs e){var row=ItemAt(e.OriginalSource as DependencyObject);string target=row==null?browser==null?"":browser.Current:Directory.Exists(row.Path)?row.Path:"";e.Effects=DragTransfer.Effect(e,target);e.Handled=true;};list.Drop+=delegate(object s,DragEventArgs e){var row=ItemAt(e.OriginalSource as DependencyObject);string target=row==null?browser==null?"":browser.Current:Directory.Exists(row.Path)?row.Path:"";DragTransfer.Drop(this,c,e,target,delegate{Refresh(search.Text);});};Refresh();
    }
    static DetailRow ItemAt(DependencyObject part){while(part!=null){var item=part as ListViewItem;if(item!=null)return item.Content as DetailRow;part=part is Visual?VisualTreeHelper.GetParent(part):LogicalTreeHelper.GetParent(part);}return null;}
    void Refresh(){Refresh("");}void Refresh(string search){var current=controller.Store.Data.Groups.FirstOrDefault(g=>g.Id==group.Id);string selected=(list.SelectedItem as DetailRow)==null?initialSelection:((DetailRow)list.SelectedItem).Path;var rows=current==null?new DetailRow[0]:controller.Store.ItemsAt(current,search,browser==null?"":browser.Current).Take(1000).Select(p=>new DetailRow(){Path=p,ShowExtension=controller.Store.Data.ShowFileExtensions}).ToArray();list.ItemsSource=rows;list.SelectedItem=rows.FirstOrDefault(r=>r.Path==selected)??rows.FirstOrDefault(r=>new[]{".png",".jpg",".jpeg",".webp",".bmp",".gif"}.Contains(Path.GetExtension(r.Path).ToLowerInvariant()))??rows.FirstOrDefault();}
}
}
