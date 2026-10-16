using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace QiDesk {
public static class WorkspaceSelection {
    public static bool Available(string path){try{return !String.IsNullOrEmpty(path)&&WorkspaceConnections.SafeDirectory(path);}catch{return false;}}
    public static string Current(State state){
        if(!String.IsNullOrEmpty(state.CurrentWorkspace)&&state.WorkFolders.Contains(state.CurrentWorkspace,StringComparer.OrdinalIgnoreCase))return state.CurrentWorkspace;
        return state.WorkFolders.AsEnumerable().Reverse().OrderByDescending(p=>state.WorkConnectedAt.ContainsKey(p)?state.WorkConnectedAt[p]:"").FirstOrDefault()??"";
    }
    public static void Select(State state,string path){
        string registered=state.WorkFolders.FirstOrDefault(p=>String.Equals(IgnoreRule.Canonical(p),IgnoreRule.Canonical(path),StringComparison.OrdinalIgnoreCase));
        if(registered==null||!Available(registered))throw new InvalidDataException("请先连接一个可用的项目目录。");
        state.CurrentWorkspace=registered;
    }
    public static object Describe(State state){string path=Current(state);return new {workspaceId=state.WorkFolders.FindIndex(p=>String.Equals(p,path,StringComparison.OrdinalIgnoreCase)).ToString(),name=String.IsNullOrEmpty(path)?"":FilePresentation.FolderName(path),path=path,available=Available(path)};}
}
public partial class ManagerWindow {
    WorkspaceSearchWindow workspaceSearch;
    readonly List<WorkspaceSearchWindow> searchPanels=new List<WorkspaceSearchWindow>();
    public void ShowWorkspaceSearch(bool toggle=false,bool allFiles=false){
        if(workspaceSearch==null){var panel=new WorkspaceSearchWindow(controller);workspaceSearch=panel;searchPanels.Add(panel);panel.Closed+=delegate{searchPanels.Remove(panel);if(workspaceSearch==panel)workspaceSearch=null;};}
        if(toggle&&workspaceSearch.IsVisible){workspaceSearch.Dismiss();return;}
        workspaceSearch.Present(allFiles);
    }
    public void NewWorkspaceSearch(){var panel=new WorkspaceSearchWindow(controller);searchPanels.Add(panel);panel.Closed+=delegate{searchPanels.Remove(panel);};panel.Present(false);panel.Left+=Math.Min(24*(searchPanels.Count-1),120);panel.Top+=Math.Min(24*(searchPanels.Count-1),120);}
    void CloseWorkspaceSearch(){foreach(var panel in searchPanels.ToArray())panel.Close();}
}
// Alt+Space 面板：顶部是分区标签栏（桌面分区/忽略/最近），下方是与桌面一致的文件网格；
// Ctrl+F 或直接输入进入搜索。把文件夹拖到顶部空白处会新建分区，拖文件到某个分区标签会加入该分区。
public sealed class WorkspaceSearchWindow : Window {
    readonly Controller controller;
    readonly TextBox input;
    readonly TextBlock placeholder,summary,scopeTitle;
    readonly ListBox results;
    readonly ComboBox projects;
    readonly Button projectButton,allButton;
    readonly List<Tuple<Button,string>> filters=new List<Tuple<Button,string>>();
    readonly DispatcherTimer debounce;
    readonly StackPanel tabBar;
    readonly ScrollViewer tabScroll;
    string workspace="",filter="",activeTab="全部",tabSignature="";
    bool allFiles,refreshing,busy,closed;
    int generation;
    public TextBox SearchInput {get{return input;}}
    public ListBox ResultList {get{return results;}}
    public string SearchScope {get{return allFiles?"":workspace;}}
    public WorkspaceSearchWindow(Controller owner){
        controller=owner;Title="栖桌 · 文件面板";Width=760;Height=620;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;Topmost=true;FontFamily=new FontFamily("Microsoft YaHei UI");Foreground=Ui.Ink;
        var frame=new Border(){Margin=new Thickness(18),Padding=new Thickness(24),CornerRadius=new CornerRadius(28),Background=Ui.Brush("#F7F9FC"),BorderBrush=Brushes.White,BorderThickness=new Thickness(1),Effect=Ui.Shadow(5,24,.24)};Content=frame;
        var layout=new DockPanel();frame.Child=layout;
        var top=new StackPanel();DockPanel.SetDock(top,Dock.Top);layout.Children.Add(top);
        var heading=new DockPanel();top.Children.Add(heading);
        var close=Ui.Button("Esc",Dismiss);close.Padding=new Thickness(10,4,10,4);close.FontSize=11;DockPanel.SetDock(close,Dock.Right);heading.Children.Add(close);
        var newPanel=Ui.Button("＋ 新面板",delegate{controller.Manager.NewWorkspaceSearch();});newPanel.Padding=new Thickness(10,4,10,4);newPanel.FontSize=11;DockPanel.SetDock(newPanel,Dock.Right);heading.Children.Add(newPanel);
        scopeTitle=Ui.Text("文件面板",18,Ui.Ink);scopeTitle.FontWeight=FontWeights.SemiBold;heading.Children.Add(scopeTitle);
        heading.MouseLeftButtonDown+=delegate(object s,MouseButtonEventArgs e){if(e.OriginalSource==scopeTitle&&e.LeftButton==MouseButtonState.Pressed)DragMove();};
        LocationChanged+=delegate{if(!IsVisible||Left< -1000||Top< -1000)return;controller.Store.Data.SearchPanelX=Left;controller.Store.Data.SearchPanelY=Top;controller.Store.Data.SearchPanelPositioned=true;controller.Store.Save();};
        // 顶部：分区标签栏。空白处接收拖入的文件夹以新建分区，右键可快捷添加。
        tabBar=new StackPanel(){Orientation=Orientation.Horizontal};
        tabScroll=new ScrollViewer(){Content=tabBar,HorizontalScrollBarVisibility=ScrollBarVisibility.Auto,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,Margin=new Thickness(0,10,0,4),Padding=new Thickness(2),MinHeight=40,AllowDrop=true};
        tabScroll.DragOver+=delegate(object s,DragEventArgs e){e.Effects=Functions.DragPaths(e.Data)!=null?DragDropEffects.Link:DragDropEffects.None;e.Handled=true;};
        tabScroll.Drop+=delegate(object s,DragEventArgs e){e.Handled=true;DropOnTab(null,Functions.DragPaths(e.Data));};
        tabScroll.ContextMenu=new ContextMenu();
        tabScroll.ContextMenu.Items.Add(Ui.Menu("新建桌面分区",delegate{if(controller.Manager!=null)controller.Manager.CreateTabGroup();}));
        tabScroll.ContextMenu.Items.Add(Ui.Menu("新建文件夹映射…",delegate{NewFolderTab();}));
        tabScroll.ContextMenu.Items.Add(Ui.Menu("刷新列表",delegate{tabSignature="";RefreshTabs();Schedule();}));
        top.Children.Add(tabScroll);
        var searchBar=new Border(){CornerRadius=new CornerRadius(24),Background=Ui.Brush("#EAF0F7"),Padding=new Thickness(17,12,12,12),Margin=new Thickness(0,8,0,12)};
        var searchLayout=new DockPanel();searchBar.Child=searchLayout;top.Children.Add(searchBar);
        var magnifier=new TextBlock(){Text="⌕",FontFamily=new FontFamily("Segoe UI Symbol"),FontSize=26,Foreground=Ui.Accent,Width=34,VerticalAlignment=VerticalAlignment.Center};DockPanel.SetDock(magnifier,Dock.Left);searchLayout.Children.Add(magnifier);
        var clear=Ui.Button("×",delegate{input.Clear();input.Focus();});clear.Margin=new Thickness(8,0,0,0);clear.Padding=new Thickness(7,0,7,0);clear.Background=Brushes.Transparent;clear.BorderThickness=new Thickness(0);DockPanel.SetDock(clear,Dock.Right);searchLayout.Children.Add(clear);
        var entry=new Grid();searchLayout.Children.Add(entry);
        input=new TextBox(){BorderThickness=new Thickness(0),Background=Brushes.Transparent,Foreground=Ui.Ink,FontSize=20,Padding=new Thickness(0,3,0,3),MaxLength=1024,VerticalContentAlignment=VerticalAlignment.Center};entry.Children.Add(input);
        placeholder=new TextBlock(){Text="搜索文件、文档、图片…  Ctrl＋F",FontSize=19,Foreground=Ui.Muted,VerticalAlignment=VerticalAlignment.Center,IsHitTestVisible=false};entry.Children.Add(placeholder);
        var scope=new DockPanel(){Margin=new Thickness(0,0,0,10)};top.Children.Add(scope);
        projectButton=Ui.Button("当前项目",delegate{SetScope(false);});allButton=Ui.Button("全盘文件",delegate{SetScope(true);});projectButton.FontSize=12;allButton.FontSize=12;DockPanel.SetDock(projectButton,Dock.Left);scope.Children.Add(projectButton);DockPanel.SetDock(allButton,Dock.Left);scope.Children.Add(allButton);
        projects=new ComboBox(){MinWidth=130,MaxWidth=270,FontSize=12,Padding=new Thickness(8,4,8,4),Margin=new Thickness(4,0,0,8),VerticalAlignment=VerticalAlignment.Center,DisplayMemberPath="Name",SelectedValuePath="Path",Background=Ui.Surface};scope.Children.Add(projects);
        projects.SelectionChanged+=delegate{if(refreshing)return;var selected=projects.SelectedItem as SearchProject;if(selected==null)return;try{controller.Store.Checkpoint();WorkspaceSelection.Select(controller.Store.Data,selected.Path);controller.Store.Save();workspace=selected.Path;SetScope(false);}catch(Exception ex){summary.Text=ex.Message;}};
        var chips=new WrapPanel();top.Children.Add(chips);
        AddFilter(chips,"全部","");AddFilter(chips,"文档","ext:pdf;doc;docx;txt;md;xls;xlsx;ppt;pptx");AddFilter(chips,"图片","ext:png;jpg;jpeg;gif;webp;bmp;svg");AddFilter(chips,"文件夹","folder:");AddFilter(chips,"今天","dm:today");
        summary=Ui.Text("顶部选择分区，或直接输入名称搜索",11,Ui.Muted);summary.Margin=new Thickness(2,3,0,8);top.Children.Add(summary);
        var footer=Ui.Text("Ctrl＋F 搜索     ↑ ↓ 选择     Enter 打开     Ctrl＋Enter 定位     Ctrl＋N 新面板     右键标签添加分区",11,Ui.Muted);footer.Margin=new Thickness(2,12,0,0);DockPanel.SetDock(footer,Dock.Bottom);layout.Children.Add(footer);
        results=new ListBox(){Background=Brushes.Transparent,BorderThickness=new Thickness(0),HorizontalContentAlignment=HorizontalAlignment.Left,Padding=new Thickness(0)};ScrollViewer.SetHorizontalScrollBarVisibility(results,ScrollBarVisibility.Disabled);results.ItemsPanel=(ItemsPanelTemplate)System.Windows.Markup.XamlReader.Parse("<ItemsPanelTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><WrapPanel/></ItemsPanelTemplate>");layout.Children.Add(results);
        results.ItemContainerStyle=(Style)System.Windows.Markup.XamlReader.Parse("<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ListBoxItem'><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ListBoxItem'><Border x:Name='tile' CornerRadius='12' Padding='{TemplateBinding Padding}' Margin='0,0,0,3' Background='Transparent'><ContentPresenter/></Border><ControlTemplate.Triggers><Trigger Property='IsSelected' Value='True'><Setter TargetName='tile' Property='Background' Value='#E3EDF9'/></Trigger><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='tile' Property='Background' Value='#EDF2F8'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>");
        debounce=new DispatcherTimer(){Interval=TimeSpan.FromMilliseconds(220)};debounce.Tick+=delegate{debounce.Stop();SearchLatest();};
        input.TextChanged+=delegate{placeholder.Visibility=input.Text.Length==0?Visibility.Visible:Visibility.Collapsed;Schedule();};
        PreviewKeyDown+=OnKey;
        IsVisibleChanged+=delegate{if(!IsVisible){generation++;debounce.Stop();}};
        Activated+=delegate{RefreshTabs();};
        Closed+=delegate{closed=true;generation++;debounce.Stop();};
        UpdateScope();RefreshTabs();
    }
    void AddFilter(Panel panel,string label,string query){Button button=null;button=Ui.Button(label,delegate{filter=query;UpdateFilters();Schedule();input.Focus();});button.FontSize=11;button.Padding=new Thickness(12,5,12,5);panel.Children.Add(button);filters.Add(Tuple.Create(button,query));}
    void UpdateFilters(){foreach(var chip in filters){chip.Item1.Background=chip.Item2==filter?Ui.Brush("#DDEBFA"):Ui.Surface;chip.Item1.Foreground=chip.Item2==filter?Ui.Accent:Ui.Muted;}}
    void SetScope(bool value){allFiles=value;UpdateScope();Schedule();input.Focus();}
    void UpdateScope(){projectButton.Background=allFiles?Ui.Surface:Ui.Brush("#DDEBFA");allButton.Background=allFiles?Ui.Brush("#DDEBFA"):Ui.Surface;projects.Visibility=allFiles?Visibility.Collapsed:Visibility.Visible;scopeTitle.Text=allFiles?"文件搜索":"文件面板";UpdateFilters();}
    // ---- 分区标签栏 ----
    void RefreshTabs(){
        if(tabBar==null)return;
        var groups=controller.Store.Data.Groups.Where(g=>String.IsNullOrEmpty(g.HostGroupId)).ToArray();
        if(activeTab!="全部"&&activeTab!="忽略"&&activeTab!="最近"&&!groups.Any(g=>g.Id==activeTab))activeTab="全部";
        string signature=String.Join("|",groups.Select(g=>g.Id+":"+g.Name))+"#"+activeTab+"#"+controller.Store.Data.Page;
        if(signature==tabSignature)return;tabSignature=signature;
        tabBar.Children.Clear();
        tabBar.Children.Add(MakeTab("全部","全部",Ui.Accent));
        foreach(var group in groups)tabBar.Children.Add(MakeTab(group.Name,group.Id,Ui.Brush(group.Color)));
        tabBar.Children.Add(MakeTab("忽略","忽略",Ui.Brush("#C08A5A")));
        tabBar.Children.Add(MakeTab("最近","最近",Ui.Brush("#8A7FC0")));
        var add=Ui.Button("＋",delegate{NewFolderTab();});add.Padding=new Thickness(8,4,8,4);add.FontSize=13;add.Margin=new Thickness(4,0,0,0);tabBar.Children.Add(add);
    }
    Border MakeTab(string label,string key,Brush accent){
        bool active=key==activeTab;
        var text=Ui.Text(label,12,active?Ui.Ink:Ui.Muted);text.Margin=new Thickness(0);text.TextTrimming=TextTrimming.CharacterEllipsis;text.FontWeight=active?FontWeights.SemiBold:FontWeights.Normal;
        var tab=new Border(){Child=text,Cursor=Cursors.Hand,Padding=new Thickness(12,5,12,5),Margin=new Thickness(0,0,6,0),CornerRadius=new CornerRadius(14),Background=active?Ui.Brush("#DDEBFA"):Ui.Surface,BorderBrush=active?accent:Ui.Brush("#D7DEE6"),BorderThickness=new Thickness(active?1.5:1),ToolTip=label};
        tab.MouseLeftButtonDown+=delegate(object s,MouseButtonEventArgs e){e.Handled=true;SelectTab(key);};
        tab.AllowDrop=true;
        tab.DragOver+=delegate(object s,DragEventArgs e){e.Effects=Functions.DragPaths(e.Data)!=null?DragDropEffects.Link:DragDropEffects.None;e.Handled=true;};
        tab.Drop+=delegate(object s,DragEventArgs e){e.Handled=true;DropOnTab(key,Functions.DragPaths(e.Data));};
        var menu=new ContextMenu();
        menu.Items.Add(Ui.Menu("显示此分区",delegate{SelectTab(key);}));
        var group=controller.Store.Data.Groups.FirstOrDefault(g=>g.Id==key);
        if(group!=null){menu.Items.Add(Ui.Menu("重命名分区",delegate{if(controller.Manager!=null)controller.Manager.EditGroup(group);}));menu.Items.Add(Ui.Menu("删除分区",delegate{if(controller.Manager!=null)controller.Manager.DeleteTabGroup(group);}));}
        menu.Items.Add(Ui.Menu("新建桌面分区",delegate{if(controller.Manager!=null)controller.Manager.CreateTabGroup();}));
        menu.Items.Add(Ui.Menu("新建文件夹映射…",delegate{NewFolderTab();}));
        tab.ContextMenu=menu;
        return tab;
    }
    void SelectTab(string key){activeTab=key;filter="";UpdateFilters();if(input.Text.Length>0)input.Clear();else Schedule();tabSignature="";RefreshTabs();}
    IEnumerable<string> TabItems(string key){
        var store=controller.Store;
        if(key=="忽略")return IgnoredItems();
        if(key=="最近")return FileActivity.Recent(store).Select(e=>e.Path);
        if(key=="全部"){
            var set=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(string path in Store.DesktopItems())set.Add(path);
            foreach(var g in store.Data.Groups.Where(g=>String.IsNullOrEmpty(g.Folder)))foreach(string path in store.Items(g,""))set.Add(path);
            return set;
        }
        var group=store.Data.Groups.FirstOrDefault(g=>g.Id==key);
        return group==null?new string[0]:store.Items(group,"");
    }
    IEnumerable<string> IgnoredItems(){
        var store=controller.Store;var set=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(string path in Store.DesktopItems())if(store.Ignored(path,null))set.Add(path);
        foreach(var g in store.Data.Groups.Where(g=>String.IsNullOrEmpty(g.Folder)))foreach(string path in g.Items)if(store.Ignored(path,g))set.Add(path);
        return set;
    }
    Group ActiveGroup(){return controller.Store.Data.Groups.FirstOrDefault(g=>g.Id==activeTab);}
    void RenderActiveTab(){
        if(refreshing||closed)return;generation++;
        results.Items.Clear();
        var paths=TabItems(activeTab).Where(Store.Exists).OrderBy(Path.GetFileName,StringComparer.CurrentCultureIgnoreCase).Take(300).ToArray();
        foreach(string path in paths)results.Items.Add(MakeTile(Describe(path),ActiveGroup()));
        if(results.Items.Count>0)results.SelectedIndex=0;
        summary.Text=activeTab=="忽略"?"已忽略的项目 · "+paths.Length+" 项（右键可取消忽略）":Tabs2Label(activeTab)+" · "+paths.Length+" 项 · "+TabHint(activeTab);
    }
    string Tabs2Label(string key){if(key=="全部")return "全部桌面项目";if(key=="最近")return "最近打开";if(key=="忽略")return "忽略";var g=controller.Store.Data.Groups.FirstOrDefault(x=>x.Id==key);return g==null?"分区":g.Name;}
    string TabHint(string key){if(key=="忽略")return "这些项目按忽略规则隐藏，未从分区移除。";if(key=="最近")return "来自最近打开记录。";var g=controller.Store.Data.Groups.FirstOrDefault(x=>x.Id==key);if(g!=null&&!String.IsNullOrEmpty(g.Folder))return "文件夹映射 · 双击文件夹可继续进入。";return "拖文件到这里加入，或把文件夹拖到顶栏新建分区。";}
    SearchResult Describe(string path){return new SearchResult(){Path=path,Bytes=FilePresentation.Bytes(path),Changed=Modified(path)};}
    static DateTime Modified(string path){try{return File.GetLastWriteTime(path);}catch{return DateTime.MinValue;}}
    void NewFolderTab(){
        using(var dialog=new System.Windows.Forms.FolderBrowserDialog()){dialog.Description="选择要作为分区显示的文件夹";if(dialog.ShowDialog()!=System.Windows.Forms.DialogResult.OK)return;DropOnTab(null,new[]{dialog.SelectedPath});}
    }
    // 落点：文件夹 -> 新建/选中文件夹分区；文件 -> 加入当前分区（没有则新建一个普通分区）。
    void DropOnTab(string key,string[] paths){
        if(paths==null||paths.Length==0)return;
        var store=controller.Store;var directories=paths.Where(Directory.Exists).ToArray();var files=paths.Where(File.Exists).ToArray();string last=key;
        try{
            if(directories.Length>0){
                store.Checkpoint();
                foreach(string directory in directories){var created=AddFolderTab(directory);if(created!=null)last=created.Id;}
                activeTab=last??"全部";controller.SaveAndRefresh(false);tabSignature="";RefreshTabs();
            }
            if(files.Length>0){
                Group target=ActiveGroup()??store.Data.Groups.FirstOrDefault(g=>String.IsNullOrEmpty(g.Folder));
                if(target!=null&&String.IsNullOrEmpty(target.Folder)){store.Checkpoint();foreach(string file in files)store.Assign(file,target);controller.SaveAndRefresh(false);}
            }
        }catch(Exception ex){summary.Text="添加分区未完成："+ex.Message;return;}
        if(input.Text.Length>0)input.Clear();else Schedule();
    }
    public void Present(bool global,bool preview=false){
        refreshing=true;workspace=WorkspaceSelection.Current(controller.Store.Data);allFiles=global;filter="";activeTab="全部";
        var list=controller.Store.Data.WorkFolders.Select(p=>new SearchProject(){Path=p,Name=FilePresentation.FolderName(p)}).ToArray();projects.ItemsSource=list;projects.SelectedItem=list.FirstOrDefault(p=>String.Equals(p.Path,workspace,StringComparison.OrdinalIgnoreCase));refreshing=false;UpdateScope();input.Clear();
        var screen=System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position);var area=screen.WorkingArea;double scale=Shell.WindowScale(controller.Manager);Width=Math.Max(360,Math.Min(760,area.Width/scale-20));Height=Math.Max(320,Math.Min(620,area.Height/scale-20));if(controller.Store.Data.SearchPanelPositioned){Left=controller.Store.Data.SearchPanelX;Top=controller.Store.Data.SearchPanelY;}else{Left=area.Left/scale+(area.Width/scale-Width)/2;Top=area.Top/scale+Math.Max(10,(area.Height/scale-Height)*.26);}
        if(preview){Left=-20000;Top=-20000;ShowActivated=false;Topmost=false;}
        tabSignature="";RefreshTabs();Show();Schedule();
        if(!preview){Activate();input.Focus();Keyboard.Focus(input);}
    }
    public void Dismiss(){Hide();}
    void Schedule(){
        if(refreshing||closed)return;generation++;debounce.Stop();string query=(filter+" "+input.Text.Trim()).Trim();
        if(query.Length==0){RenderActiveTab();return;}
        results.Items.Clear();summary.Text="正在查找…";if(IsVisible)debounce.Start();
    }
    async void SearchLatest(){
        if(busy||closed||!IsVisible)return;
        busy=true;
        try{
            while(!closed&&IsVisible){
                int token=generation;string root=allFiles?null:workspace;string query=(filter+" "+input.Text.Trim()).Trim();
                if(query.Length==0)break;
                if(root!=null&&!WorkspaceSelection.Available(root)){summary.Text=String.IsNullOrEmpty(root)?"还没有连接项目。可在 AI 工作目录中添加，或让 AI 连接当前项目。":"项目目录暂不可用，请切换其他项目。";results.Items.Clear();break;}
                State snapshot=Store.Json.Deserialize<State>(Store.Json.Serialize(controller.Store.Data));
                var response=await Task.Run(()=>FileSearch.Query(new StoreView(controller.Store.Root,snapshot).Store,query,String.IsNullOrWhiteSpace(query),60,root));
                if(closed||!IsVisible)return;
                if(token!=generation){await Task.Delay(220);continue;}
                RenderResults(response,root);break;
            }
        }catch(Exception ex){if(IsVisible&&!closed)summary.Text="搜索未完成："+ex.Message;}
        finally{busy=false;}
    }
    public void RenderResults(SearchResponse response,string root){
        results.Items.Clear();summary.Text=(response.Provider=="Everything"?"Everything":"本机搜索")+" · "+response.Items.Length+" 项"+(response.Items.Length==0?" · 换个名称试试":"")+(response.Provider=="Everything"?"":root==null?" · 连接 Everything 可搜索全盘":" · 当前项目");summary.ToolTip=response.Message;
        foreach(var item in response.Items)results.Items.Add(MakeTile(item,null));
        if(results.Items.Count>0)results.SelectedIndex=0;
    }
    ListBoxItem MakeTile(SearchResult item,Group group){
        var tile=new StackPanel(){Width=94,Margin=new Thickness(2,4,2,4),HorizontalAlignment=HorizontalAlignment.Center};
        tile.Children.Add(new ThumbnailImage(){FilePath=item.Path,Width=30,Height=30,HorizontalAlignment=HorizontalAlignment.Center});
        tile.Children.Add(new TextBlock(){Text=item.Name,FontSize=11,Foreground=Ui.Ink,TextTrimming=TextTrimming.CharacterEllipsis,TextAlignment=TextAlignment.Center,Margin=new Thickness(0,5,0,0),ToolTip=item.Path});
        var entry=new ListBoxItem(){Content=tile,Tag=item,Padding=new Thickness(3),Width=104,Height=76,ToolTip=item.Path};
        entry.MouseDoubleClick+=delegate{OpenSelected(false);};
        var menu=new ContextMenu();
        menu.Items.Add(Ui.Menu("打开",delegate{results.SelectedItem=entry;OpenSelected(false);}));
        menu.Items.Add(Ui.Menu("定位文件",delegate{results.SelectedItem=entry;OpenSelected(true);}));
        if(group!=null&&String.IsNullOrEmpty(group.Folder)){menu.Items.Add(Ui.Menu("仅在此分区忽略",delegate{controller.Manager.IgnoreItem(item.Path,group);Schedule();}));}
        menu.Items.Add(Ui.Menu("全局忽略此项目",delegate{controller.Manager.IgnoreItem(item.Path,null);Schedule();}));
        if(controller.Store.Ignored(item.Path,group))menu.Items.Add(Ui.Menu("取消忽略",delegate{controller.Manager.UnignoreItem(item.Path);Schedule();}));
        var move=new MenuItem(){Header="移动到分组"};foreach(var candidate in controller.Store.Data.Groups.Where(g=>String.IsNullOrEmpty(g.Folder))){var target=candidate;move.Items.Add(Ui.Menu(candidate.Name,delegate{controller.Store.Checkpoint();controller.Store.Assign(item.Path,target);controller.SaveAndRefresh();Schedule();}));}menu.Items.Add(move);
        if(group!=null&&String.IsNullOrEmpty(group.Folder))menu.Items.Add(Ui.Menu("移出此分组",delegate{controller.MoveOut(item.Path,group);Schedule();}));
        entry.ContextMenu=menu;
        return entry;
    }
    public void ActivateTab(string key){SelectTab(key);}
    // 拖入/选择的文件夹会成为新的文件夹分区；已存在则直接选中。供拖放与测试复用。
    public Group AddFolderTab(string directory){
        string full;try{full=Path.GetFullPath(directory);}catch{return null;}
        if(!Directory.Exists(full))return null;
        var store=controller.Store;var existing=store.Data.Groups.FirstOrDefault(g=>String.Equals(IgnoreRule.Canonical(g.Folder),IgnoreRule.Canonical(full),StringComparison.OrdinalIgnoreCase));
        if(existing==null){existing=new Group(){Name=FilePresentation.FolderName(full),Kind="文件夹",Folder=full,Folders=new List<string>{full},Page=store.Data.Page,X=80,Y=80};store.Data.Groups.Add(existing);}
        activeTab=existing.Id;return existing;
    }
    public string[] TabPaths(string key){return TabItems(key).ToArray();}
    void OnKey(object sender,KeyEventArgs e){
        if(e.Key==Key.Escape){Dismiss();e.Handled=true;}
        else if(e.Key==Key.F&&(Keyboard.Modifiers&ModifierKeys.Control)!=0){input.Focus();Keyboard.Focus(input);e.Handled=true;}
        else if(e.Key==Key.N&&(Keyboard.Modifiers&ModifierKeys.Control)!=0){controller.Manager.NewWorkspaceSearch();e.Handled=true;}
        else if((e.Key==Key.Down||e.Key==Key.Up)&&!projects.IsDropDownOpen){if(results.Items.Count>0){int index=results.SelectedIndex+(e.Key==Key.Down?1:-1);results.SelectedIndex=Math.Max(0,Math.Min(results.Items.Count-1,index));results.ScrollIntoView(results.SelectedItem);}e.Handled=true;}
        else if(e.Key==Key.Enter&&!projects.IsDropDownOpen){OpenSelected((Keyboard.Modifiers&ModifierKeys.Control)!=0);e.Handled=true;}
    }
    void OpenSelected(bool reveal){var row=results.SelectedItem as ListBoxItem;var item=row==null?null:row.Tag as SearchResult;if(item==null)return;try{if(!Store.Exists(item.Path))throw new FileNotFoundException("文件已被移动，请重新搜索。");if(reveal)Shell.Reveal(item.Path);else Shell.Open(item.Path);Dismiss();}catch(Exception ex){summary.Text=ex.Message;}}
    sealed class SearchProject {public string Path {get;set;}public string Name {get;set;}}
}
}
