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
using System.Windows.Interop;

namespace QiDesk {
// 工作区面板：参考 PathCat 的文件侧栏，做成栖桌风格的面板。
// 顶部是若干个标签（真实文件夹，或桌面上的分区），一次只显示一个；
// 面板可以在桌面之外浮动，也可以「钉到桌面」变回普通分区。
public sealed class WorkspacePanel : Window {
    const double HeaderHeight=46;
    const double PanelMinWidth=340;
    const double PanelMinHeight=260;
    // Ctrl＋滚轮的五档缩放：大图标 → 中 → 小 → 列表 → 可折叠树。
    public static readonly string[] ZoomLadder={"图标:96","图标:72","图标:56","图标:44","图标:36","图标:28","详细信息","列表","树"};
    readonly Controller controller;
    readonly Border frame;
    readonly StackPanel tabBar,rows;
    readonly WrapPanel tiles;
    readonly ScrollViewer tabScroll,scroll;
    readonly TextBox searchBox;
    readonly Border searchBar;
    readonly TextBlock titleText,summary,hint,emptyText;
    readonly Border ignoreTarget;
    Border addTab;
    readonly TextBlock ignoreIcon;
    readonly List<PortalSession> sessions=new List<PortalSession>();
    readonly List<string> clipboard=new List<string>();
    readonly List<string> selection=new List<string>();
    readonly HashSet<string> expanded=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    readonly DispatcherTimer searchDebounce;
    bool clipboardCut,closed,suppressSearch,applyingBounds;
    int generation;
    string lastSignature,lastTabSignature;
    public WorkspacePanel(Controller owner){
        controller=owner;
        Title="栖桌 · 工作区面板";WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;
        Background=Brushes.Transparent;ShowInTaskbar=false;Topmost=true;MinWidth=PanelMinWidth;MinHeight=PanelMinHeight;
        FontFamily=new FontFamily("Microsoft YaHei UI");Foreground=Ui.DesktopInk;
        frame=new Border(){BorderThickness=new Thickness(1.5),Effect=Ui.Shadow(6,26,.34)};
        var grid=new Grid();
        grid.RowDefinitions.Add(new RowDefinition(){Height=new GridLength(HeaderHeight)});
        grid.RowDefinitions.Add(new RowDefinition(){Height=GridLength.Auto});
        grid.RowDefinitions.Add(new RowDefinition(){Height=new GridLength(1,GridUnitType.Star)});
        grid.RowDefinitions.Add(new RowDefinition(){Height=GridLength.Auto});
        // ---- 顶部：标题、标签、忽略图标、小猫开关、图钉、关闭 ----
        var header=new Grid(){Margin=new Thickness(12,7,10,4)};
        header.ColumnDefinitions.Add(new ColumnDefinition(){Width=GridLength.Auto});
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition(){Width=GridLength.Auto});
        titleText=Ui.Text("工作区面板",13,Ui.DesktopMuted);titleText.Margin=new Thickness(0);titleText.VerticalAlignment=VerticalAlignment.Center;titleText.Cursor=Cursors.SizeAll;titleText.ToolTip="按住拖动面板";
        header.Children.Add(titleText);
        tabBar=new StackPanel(){Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};
        tabScroll=new ScrollViewer(){Content=tabBar,HorizontalScrollBarVisibility=ScrollBarVisibility.Hidden,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,Margin=new Thickness(10,0,8,0),VerticalAlignment=VerticalAlignment.Center,AllowDrop=true};
        tabScroll.PreviewMouseWheel+=delegate(object s,MouseWheelEventArgs e){tabScroll.ScrollToHorizontalOffset(tabScroll.HorizontalOffset-Math.Sign(e.Delta)*90);e.Handled=true;};
        tabScroll.DragOver+=delegate(object s,DragEventArgs e){e.Effects=HasDirectory(e.Data)?DragDropEffects.Link:DragDropEffects.None;e.Handled=true;};
        tabScroll.Drop+=delegate(object s,DragEventArgs e){e.Handled=true;AddFolders(Functions.DragPaths(e.Data));};
        tabScroll.MouseEnter+=delegate{if(addTab!=null){addTab.Opacity=1;addTab.IsHitTestVisible=true;}};
        tabScroll.MouseLeave+=delegate{if(addTab!=null){addTab.Opacity=0;addTab.IsHitTestVisible=false;}};
        tabScroll.ContextMenu=BuildTabBarMenu();
        Grid.SetColumn(tabScroll,1);header.Children.Add(tabScroll);
        var actions=new StackPanel(){Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center,Opacity=0,IsHitTestVisible=false};
        ignoreIcon=Ui.Text(controller.Store.Data.PanelIgnoreIcon,18,Ui.DesktopMuted);ignoreIcon.Margin=new Thickness(0);ignoreIcon.HorizontalAlignment=HorizontalAlignment.Center;ignoreIcon.VerticalAlignment=VerticalAlignment.Center;
        ignoreTarget=new Border(){Child=ignoreIcon,Width=32,Height=32,CornerRadius=new CornerRadius(8),BorderThickness=new Thickness(1),BorderBrush=Ui.Brush("#42536B"),Background=Brushes.Transparent,Margin=new Thickness(0,0,4,0),AllowDrop=true,ToolTip="把文件或文件夹拖到这里加入忽略；右键可更换图标"};
        ignoreTarget.DragOver+=delegate(object s,DragEventArgs e){bool ok=Functions.DragPaths(e.Data)!=null;e.Effects=ok?DragDropEffects.Link:DragDropEffects.None;ignoreTarget.BorderBrush=ok?Ui.Accent:Ui.Brush("#42536B");e.Handled=true;};
        ignoreTarget.DragLeave+=delegate{ignoreTarget.BorderBrush=Ui.Brush("#42536B");};
        ignoreTarget.Drop+=delegate(object s,DragEventArgs e){e.Handled=true;ignoreTarget.BorderBrush=Ui.Brush("#42536B");IgnoreDropped(Functions.DragPaths(e.Data));};
        ignoreTarget.ContextMenu=new ContextMenu();
        ignoreTarget.ContextMenuOpening+=delegate{ignoreTarget.ContextMenu=BuildIgnoreMenu();};
        actions.Children.Add(ignoreTarget);
        var petButton=Small("猫",delegate{if(controller.Manager!=null)controller.Manager.TogglePet();},"显示 / 隐藏桌面小猫");actions.Children.Add(petButton);
        var pin=Small("⌖",PinToDesktop,"钉到桌面：把面板变回一个桌面分区");actions.Children.Add(pin);
        var rescan=Small("⟳",delegate{lastSignature=null;lastTabSignature=null;Refresh();},"刷新");actions.Children.Add(rescan);
        var close=Small("×",Dismiss,"收起面板");actions.Children.Add(close);
        Grid.SetColumn(actions,2);header.Children.Add(actions);
        Action showActions=delegate{actions.Opacity=1;actions.IsHitTestVisible=true;};
        Action hideActions=delegate{actions.Opacity=0;actions.IsHitTestVisible=false;};
        header.MouseEnter+=delegate{showActions();};
        header.MouseLeave+=delegate{hideActions();};
        header.DragEnter+=delegate{showActions();};
        header.DragLeave+=delegate{hideActions();};
        header.MouseLeftButtonDown+=delegate(object s,MouseButtonEventArgs e){if(Inside(e.OriginalSource,tabScroll)||Inside(e.OriginalSource,actions))return;BeginMoveWindow(e);};
        header.ContextMenu=BuildBackgroundMenu();
        Grid.SetRow(header,0);grid.Children.Add(header);
        // ---- 搜索栏（默认隐藏，Ctrl+F 展开）----
        searchBar=new Border(){CornerRadius=new CornerRadius(18),Background=Ui.Brush("#16202F"),Padding=new Thickness(12,6,8,6),Margin=new Thickness(12,0,12,6),Visibility=Visibility.Collapsed};
        var searchLayout=new DockPanel();searchBar.Child=searchLayout;
        var magnifier=new TextBlock(){Text="⌕",FontFamily=new FontFamily("Segoe UI Symbol"),FontSize=18,Foreground=Ui.Accent,Width=24,VerticalAlignment=VerticalAlignment.Center};DockPanel.SetDock(magnifier,Dock.Left);searchLayout.Children.Add(magnifier);
        var clearSearch=Ui.Button("×",delegate{searchBox.Clear();searchBox.Focus();});clearSearch.Padding=new Thickness(6,0,6,0);clearSearch.Margin=new Thickness(6,0,0,0);clearSearch.FontSize=11;DockPanel.SetDock(clearSearch,Dock.Right);searchLayout.Children.Add(clearSearch);
        searchBox=new TextBox(){BorderThickness=new Thickness(0),Background=Brushes.Transparent,Foreground=Ui.DesktopInk,CaretBrush=Ui.DesktopInk,FontSize=14,Padding=new Thickness(0,2,0,2),MaxLength=200,VerticalContentAlignment=VerticalAlignment.Center};
        searchLayout.Children.Add(searchBox);
        Grid.SetRow(searchBar,1);grid.Children.Add(searchBar);
        // ---- 内容：图标网格 / 列表 / 可折叠树 ----
        tiles=new WrapPanel(){Margin=new Thickness(10,2,8,4)};
        rows=new StackPanel(){Margin=new Thickness(4,2,4,4)};
        emptyText=Ui.Text("",12,Ui.DesktopMuted);emptyText.Margin=new Thickness(12,16,12,12);emptyText.Visibility=Visibility.Collapsed;
        var content=new StackPanel();content.Children.Add(emptyText);content.Children.Add(tiles);content.Children.Add(rows);
        scroll=new ScrollViewer(){Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Margin=new Thickness(4,0,4,0),Focusable=false};
        scroll.Loaded+=delegate{Ui.SetVerticalScrollBarOpacity(scroll,scroll.IsMouseOver?1:0);};
        scroll.MouseEnter+=delegate{Ui.SetVerticalScrollBarOpacity(scroll,1);};
        scroll.MouseLeave+=delegate{Ui.SetVerticalScrollBarOpacity(scroll,0);};
        scroll.AllowDrop=true;
        scroll.PreviewDragOver+=delegate(object s,DragEventArgs e){e.Effects=DropEffect(e);e.Handled=true;};
        scroll.Drop+=delegate(object s,DragEventArgs e){e.Handled=true;DropInto(e,CurrentFolder);};
        scroll.PreviewMouseLeftButtonDown+=delegate(object s,MouseButtonEventArgs e){if(OnItem(e.OriginalSource))return;ClearSelection();BeginMoveWindow(e);};
        scroll.ContextMenu=BuildBackgroundMenu();
        Grid.SetRow(scroll,2);grid.Children.Add(scroll);
        // ---- 底部提示 ----
        hint=Ui.Text("Ctrl＋滚轮 缩放    Ctrl＋F 搜索    Enter 打开    F2 重命名    Delete 删除    Ctrl＋C/X/V 复制剪切粘贴    拖动=移动，Ctrl＋拖动=复制    左键拖空白处移动面板",11,Ui.DesktopMuted);
        hint.Margin=new Thickness(14,4,12,2);hint.TextTrimming=TextTrimming.CharacterEllipsis;hint.Visibility=Visibility.Collapsed;
        summary=Ui.Text("",11,Ui.DesktopMuted);summary.Margin=new Thickness(14,0,12,9);summary.TextTrimming=TextTrimming.CharacterEllipsis;
        var footer=new StackPanel();footer.Children.Add(hint);footer.Children.Add(summary);
        Grid.SetRow(footer,3);grid.Children.Add(footer);
        // 左右边缘可拖动改宽
        var right=new Thumb(){Width=8,Height=72,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center,Background=Ui.Brush("#77BFA3"),Opacity=.10,Cursor=Cursors.SizeWE};
        var left=new Thumb(){Width=8,Height=72,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Center,Background=Ui.Brush("#77BFA3"),Opacity=.10,Cursor=Cursors.SizeWE};
        Grid.SetRow(right,0);Grid.SetRowSpan(right,4);Grid.SetRow(left,0);Grid.SetRowSpan(left,4);
        right.DragDelta+=delegate(object s,DragDeltaEventArgs e){Width=Math.Max(PanelMinWidth,Width+e.HorizontalChange);RememberBounds();};
        left.DragDelta+=delegate(object s,DragDeltaEventArgs e){double change=Math.Min(e.HorizontalChange,Width-PanelMinWidth);Left+=change;Width-=change;RememberBounds();};
        right.MouseEnter+=delegate{right.Opacity=.5;};right.MouseLeave+=delegate{right.Opacity=.10;};
        left.MouseEnter+=delegate{left.Opacity=.5;};left.MouseLeave+=delegate{left.Opacity=.10;};
        grid.Children.Add(right);grid.Children.Add(left);
        frame.Child=grid;Content=frame;AllowDrop=true;
        DragOver+=delegate(object s,DragEventArgs e){e.Effects=DropEffect(e);e.Handled=true;};
        Drop+=delegate(object s,DragEventArgs e){e.Handled=true;DropInto(e,CurrentFolder);};
        PreviewMouseWheel+=OnWheel;
        // ---- 交互接线 ----
        searchDebounce=new DispatcherTimer(){Interval=TimeSpan.FromMilliseconds(240)};
        searchDebounce.Tick+=delegate{searchDebounce.Stop();RunSearch();};
        searchBox.TextChanged+=delegate{if(!suppressSearch)ScheduleSearch();};
        searchBox.PreviewKeyDown+=delegate(object s,KeyEventArgs e){
            if(e.Key!=Key.Escape)return;
            e.Handled=true;
            if(searchBox.Text.Length>0)searchBox.Clear();else CloseSearch();
        };
        PreviewKeyDown+=OnKey;
        LocationChanged+=delegate{RememberBounds();};
        SizeChanged+=delegate{RememberBounds();};
        Closed+=delegate{closed=true;generation++;searchDebounce.Stop();};
        IsVisibleChanged+=delegate{if(!IsVisible){generation++;searchDebounce.Stop();}else{lastSignature=null;lastTabSignature=null;Refresh();}if(controller.Manager!=null)controller.Manager.NotifyPanelVisibility(IsVisible);};
        LoadTabs();
    }
    Border Small(string glyph,Action action,string tip){
        var text=Ui.Text(glyph,17,Ui.DesktopMuted);text.Margin=new Thickness(0);text.HorizontalAlignment=HorizontalAlignment.Center;text.VerticalAlignment=VerticalAlignment.Center;
        var button=new Border(){Child=text,Width=32,Height=32,CornerRadius=new CornerRadius(8),BorderThickness=new Thickness(1),BorderBrush=Brushes.Transparent,Background=Brushes.Transparent,Margin=new Thickness(0,0,2,0),Cursor=Cursors.Hand,ToolTip=tip};
        button.MouseEnter+=delegate{text.Foreground=Ui.DesktopInk;button.BorderBrush=Ui.Brush("#42536B");};
        button.MouseLeave+=delegate{text.Foreground=Ui.DesktopMuted;button.BorderBrush=Brushes.Transparent;};
        button.MouseLeftButtonUp+=delegate(object s,MouseButtonEventArgs e){e.Handled=true;action();};
        return button;
    }
    List<IgnoreRule> ActiveIgnoreRules{
        get{var group=CurrentGroup;if(group!=null)return group.IgnoreRules;var tab=CurrentTab;return tab==null?controller.Store.Data.IgnoreRules:tab.IgnoreRules;}
    }
    ContextMenu BuildIgnoreMenu(PanelTab selected=null){
        var menu=new ContextMenu();var tab=selected??CurrentTab;var group=tab==null||tab.IsFolder?null:controller.Store.Data.Groups.FirstOrDefault(g=>g.Id==tab.GroupId);
        var global=controller.Store.Data.IgnoreRules;
        menu.Items.Add(new MenuItem(){Header="全局规则 · "+global.Count+" 条",IsEnabled=false});
        foreach(var rule in global.Take(12).ToArray()){var current=rule;menu.Items.Add(Ui.Menu("取消全局忽略 · "+current.Pattern,delegate{global.Remove(current);controller.Store.Save();lastSignature=null;Refresh();}));}
        menu.Items.Add(new Separator());
        string scope=group!=null?group.Name:tab!=null&&tab.IsFolder?FilePresentation.FolderName(tab.Folder):"工作区面板";
        menu.Items.Add(new MenuItem(){Header="忽略规则 · "+scope,IsEnabled=false});
        var rules=group!=null?group.IgnoreRules:tab!=null?tab.IgnoreRules:controller.Store.Data.IgnoreRules;
        if(rules.Count==0)menu.Items.Add(new MenuItem(){Header="当前标签没有忽略项",IsEnabled=false});
        foreach(var rule in rules.ToArray()){
            var current=rule;
            menu.Items.Add(Ui.Menu("取消忽略 · "+current.Pattern,delegate{rules.Remove(current);controller.Store.Save();lastSignature=null;Refresh();}));
        }
        menu.Items.Add(new Separator());
        menu.Items.Add(Ui.Menu("编辑全局和当前分区规则",delegate{if(controller.Manager!=null)controller.Manager.ShowIgnoreRules(group,tab);}));
        var icons=new MenuItem(){Header="更换忽略图标"};
        foreach(string glyph in PanelFiles.IgnoreIcons){string choice=glyph;icons.Items.Add(Ui.Menu(choice,delegate{controller.Store.Data.PanelIgnoreIcon=choice;ignoreIcon.Text=choice;controller.Store.Save();}));}
        menu.Items.Add(icons);
        return menu;
    }
    static bool Inside(object source,DependencyObject ancestor){
        var node=source as DependencyObject;
        while(node!=null){if(node==ancestor)return true;node=node is Visual?VisualTreeHelper.GetParent(node):null;}
        return false;
    }
    static bool OnItem(object source){
        var node=source as DependencyObject;
        while(node!=null){var border=node as Border;if(border!=null&&border.Tag!=null)return true;node=node is Visual?VisualTreeHelper.GetParent(node):null;}
        return false;
    }
    void BeginMoveWindow(MouseButtonEventArgs e){
        if(e.LeftButton!=MouseButtonState.Pressed)return;
        try{DragMove();}catch{}
        e.Handled=true;
    }
    // ---- 标签与内容 ----
    public Group VisualStyle{
        get{
            var data=controller.Store.Data;
            if(!String.IsNullOrEmpty(data.PanelFollowGroupId)){var followed=data.Groups.FirstOrDefault(x=>x.Id==data.PanelFollowGroupId);if(followed!=null)return followed;}
            return data.PanelStyle??new Group(){CornerStyle="方角"};
        }
    }
    public PanelTab CurrentTab{get{var data=controller.Store.Data;int index=data.PanelIndex;return index>=0&&index<data.PanelTabs.Count?data.PanelTabs[index]:null;}}
    public bool IsFolderTab{get{var tab=CurrentTab;return tab!=null&&tab.IsFolder;}}
    public Group CurrentGroup{get{var tab=CurrentTab;return tab==null||tab.IsFolder?null:controller.Store.Data.Groups.FirstOrDefault(g=>g.Id==tab.GroupId);}}
    public PortalSession Session{get{int index=controller.Store.Data.PanelIndex;return index>=0&&index<sessions.Count?sessions[index]:null;}}
    public string CurrentFolder{get{var session=Session;return session==null?"":session.Current.TrimEnd(Path.DirectorySeparatorChar);}}
    public string[] FolderTabs{get{return controller.Store.Data.PanelTabs.Where(t=>t.IsFolder).Select(t=>t.Folder).ToArray();}}
    public int ActiveIndex{get{return controller.Store.Data.PanelIndex;}}
    void LoadTabs(){
        var data=controller.Store.Data;var kept=new List<PanelTab>();
        foreach(var tab in data.PanelTabs){
            if(tab.IsFolder){
                string full;try{full=Path.GetFullPath(tab.Folder);}catch{continue;}
                if(!Directory.Exists(full))continue;
                if(kept.Any(t=>t.IsFolder&&String.Equals(t.Folder,full,StringComparison.OrdinalIgnoreCase)))continue;
                tab.Folder=full;kept.Add(tab);
            }else if(data.Groups.Any(g=>g.Id==tab.GroupId)&&!kept.Any(t=>!t.IsFolder&&t.GroupId==tab.GroupId))kept.Add(tab);
        }
        data.PanelTabs=kept;
        if(data.PanelTabs.Count==0){string current=WorkspaceSelection.Current(data);if(!String.IsNullOrEmpty(current)&&Directory.Exists(current))data.PanelTabs.Add(PanelTab.OfFolder(current));}
        EnsureSessions();
    }
    void EnsureSessions(){
        var data=controller.Store.Data;
        sessions.Clear();
        foreach(var tab in data.PanelTabs){
            if(!tab.IsFolder){sessions.Add(null);continue;}
            try{if(!Directory.Exists(tab.Folder)){sessions.Add(null);continue;}sessions.Add(new PortalSession(tab.Folder));}catch{sessions.Add(null);}
        }
        while(sessions.Count<data.PanelTabs.Count)sessions.Add(null);
        if(sessions.Count==0)data.PanelIndex=-1;
        else if(data.PanelIndex<0||data.PanelIndex>=sessions.Count)data.PanelIndex=0;
        selection.RemoveAll(p=>!Store.Exists(p));
    }
    public bool AddFolder(string directory){
        if(String.IsNullOrEmpty(directory))return false;
        string full;try{full=Path.GetFullPath(directory);}catch{return false;}
        if(!Directory.Exists(full))return false;
        var data=controller.Store.Data;
        int existing=data.PanelTabs.FindIndex(t=>t.IsFolder&&String.Equals(IgnoreRule.Canonical(t.Folder),IgnoreRule.Canonical(full),StringComparison.OrdinalIgnoreCase));
        if(existing<0){data.PanelTabs.Add(PanelTab.OfFolder(full));existing=data.PanelTabs.Count-1;}
        data.PanelIndex=existing;EnsureSessions();return true;
    }
    public void AddFolders(string[] paths){
        if(paths==null)return;
        var data=controller.Store.Data;
        int before=data.PanelTabs.Count,beforeIndex=data.PanelIndex;bool changed=false;
        foreach(string path in paths)if(AddFolder(path))changed=true;
        if(!changed&&before==data.PanelTabs.Count&&beforeIndex==data.PanelIndex)return;
        lastSignature=null;lastTabSignature=null;CloseSearch();controller.Store.Save();Refresh();
        summary.Text="已打开 "+data.PanelTabs.Count+" 个标签。";
    }
    // 把桌面上的分区接进面板；面板显示期间隐藏桌面上那一份。
    public void OpenGroups(string[] groupIds){
        if(groupIds==null||groupIds.Length==0)return;
        var data=controller.Store.Data;bool changed=false;
        foreach(string id in groupIds){
            var group=data.Groups.FirstOrDefault(g=>g.Id==id);if(group==null)continue;
            if(group.Kind=="文件夹"&&group.Folders.Count>0){
                foreach(string folder in group.Folders){
                    if(AddFolder(folder))changed=true;
                    var folderTab=data.PanelTabs.FirstOrDefault(t=>t.IsFolder&&String.Equals(t.Folder,folder,StringComparison.OrdinalIgnoreCase));
                    List<IgnoreRule> rules;
                    if(folderTab!=null&&group.FolderIgnoreRules.TryGetValue(folder,out rules))folderTab.IgnoreRules=rules.ToList();
                }
                foreach(var child in data.Groups.Where(g=>g.HostGroupId==group.Id))if(!data.PanelTabs.Any(t=>!t.IsFolder&&t.GroupId==child.Id)){data.PanelTabs.Add(PanelTab.OfGroup(child.Id));changed=true;}
                if(controller.Manager!=null)controller.Manager.HideDesktopCopies(new[]{group.Id});
                continue;
            }
            if(!data.PanelTabs.Any(t=>!t.IsFolder&&t.GroupId==id)){data.PanelTabs.Add(PanelTab.OfGroup(id));changed=true;}
            data.PanelIndex=data.PanelTabs.Count-1;
        }
        if(!changed&&data.PanelIndex<0)return;
        EnsureSessions();lastSignature=null;lastTabSignature=null;controller.Store.Save();Refresh();
        controller.Manager.HideDesktopCopies(GroupTabs());
    }
    public string[] GroupTabs(){return controller.Store.Data.PanelTabs.Where(t=>!t.IsFolder).Select(t=>t.GroupId).ToArray();}
    void RemoveTab(int index){
        var data=controller.Store.Data;if(index<0||index>=data.PanelTabs.Count)return;
        var removed=data.PanelTabs[index];
        data.PanelTabs.RemoveAt(index);
        if(data.PanelIndex>=data.PanelTabs.Count)data.PanelIndex=data.PanelTabs.Count-1;
        if(data.PanelIndex<0)data.PanelIndex=data.PanelTabs.Count==0?-1:0;
        EnsureSessions();lastSignature=null;lastTabSignature=null;controller.Store.Save();Refresh();
        if(!removed.IsFolder&&controller.Manager!=null)controller.Manager.RestoreDesktopCopy(removed.GroupId);
        summary.Text="已关闭一个标签；磁盘文件没有变动。";
    }
    void SelectTab(int index){
        var data=controller.Store.Data;if(index<0||index>=data.PanelTabs.Count)return;
        var tab=data.PanelTabs[index];bool same=index==data.PanelIndex;
        if(tab.IsFolder&&same){var session=sessions[index];if(session!=null&&!String.Equals(session.Current,session.Root,StringComparison.OrdinalIgnoreCase))sessions[index]=new PortalSession(tab.Folder);}
        data.PanelIndex=index;ClearSelection();CloseSearch();lastSignature=null;controller.Store.Save();Refresh();
    }
    void MoveTab(int index,int delta){
        var data=controller.Store.Data;int target=index+delta;if(index<0||target<0||target>=data.PanelTabs.Count)return;
        var tab=data.PanelTabs[index];data.PanelTabs.RemoveAt(index);data.PanelTabs.Insert(target,tab);
        if(data.PanelIndex==index)data.PanelIndex=target;lastSignature=null;lastTabSignature=null;controller.Store.Save();Refresh();
    }
    // ---- 显示与刷新 ----
    public void Toggle(){if(IsVisible)Dismiss();else Present();}
    public void Present(){Present(false,true);}
    public void Present(bool preview){Present(preview,!preview);}
    public void Present(bool preview,bool activate){
        var data=controller.Store.Data;LoadTabs();
        CloseSearch();ClearSelection();
        applyingBounds=true;
        Width=Math.Max(PanelMinWidth,data.PanelWidth);Height=Math.Max(PanelMinHeight,data.PanelHeight);
        bool placed=false;
        if(!preview&&data.PanelPositioned){try{var areas=Shell.WorkingAreas(this);placed=areas.Any(a=>a.IntersectsWith(new Rect(data.PanelX,data.PanelY,Width,Math.Min(Height,60))));}catch{placed=false;}if(placed){Left=data.PanelX;Top=data.PanelY;}}
        if(!placed){var screen=System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position);double scale=Shell.WindowScale(this);if(scale<=0)scale=1;var area=screen.WorkingArea;Left=area.Left/scale+area.Width/scale-Width-16;Top=area.Top/scale+Math.Max(12,Math.Min(160,(area.Height/scale-Height)/4));}
        applyingBounds=false;
        if(preview){Left=-20000;Top=-20000;ShowActivated=false;Topmost=false;}
        ignoreIcon.Text=data.PanelIgnoreIcon;lastSignature=null;lastTabSignature=null;ApplyStyle();Refresh();
        Show();
        if(activate&&!preview){Activate();Focus();}
    }
    public void Dismiss(){
        if(!IsVisible)return;
        Hide();
        if(controller.Manager!=null)controller.Manager.RestoreDesktopCopies(null);
    }
    void RememberBounds(){
        if(closed||applyingBounds||!IsLoaded)return;
        try{
            var data=controller.Store.Data;data.PanelX=Left;data.PanelY=Top;data.PanelWidth=Math.Max(PanelMinWidth,Width);data.PanelHeight=Math.Max(PanelMinHeight,ActualHeight>1?ActualHeight:Height);data.PanelPositioned=true;controller.Store.Save();
        }catch{}
    }
    // 外观完全复用桌面分区那一套：颜色、色调、不透明度、边角、标签字体、图标皮肤。
    void ApplyStyle(){
        var style=VisualStyle;
        bool square=style.CornerStyle=="方角";
        frame.CornerRadius=new CornerRadius(square?0:16);
        Color tint=TimedAccent(style.Color);
        frame.BorderBrush=new SolidColorBrush(Color.FromArgb((byte)Math.Max(1,Math.Round(255*style.BorderOpacity)),tint.R,tint.G,tint.B));
        frame.Background=Appearance.FrameBackground(style,style.BackgroundOpacity);
        titleText.Foreground=Ui.Brush(style.LabelColor);
        titleText.FontFamily=new FontFamily(String.IsNullOrWhiteSpace(style.LabelFont)?"Microsoft YaHei UI":style.LabelFont);
    }
    static Color TimedAccent(string color){try{return (Color)new BrushConverter().ConvertFromString(color);}catch{return Color.FromRgb(146,170,229);}}
    void Refresh(){
        if(closed)return;
        RefreshTabs();
        var data=controller.Store.Data;var tab=CurrentTab;
        titleText.Text="工作区面板";titleText.Visibility=data.PanelTabs.Count==0?Visibility.Visible:Visibility.Collapsed;
        if(tab==null){
            ShowEmpty(data.PanelTabs.Count==0?"把文件夹拖到顶部栏位，或点 ＋ 添加一个标签。":"这些标签暂时不可用，用 ＋ 重新添加一个。");
            return;
        }
        string[] items=TabItems(tab);
        if(items==null){ShowEmpty("这个标签的内容暂时打不开。");return;}
        string folder=tab.IsFolder?CurrentFolder:"";
        string signature=String.Join("\n",items.Select(p=>p+"|"+Thumbnails.Revision(p)))+"|"+data.PanelViewMode+"|"+data.PanelIconSize+"|"+data.ShowFileExtensions+"|"+folder+"|"+Width+"|"+tab.GroupId+"|"+expanded.Count;
        bool tree=data.PanelViewMode=="树";
        if(!tree&&IsFolderTab)signature+="|"+String.Equals(CurrentFolder,Session==null?"":Session.Root,StringComparison.OrdinalIgnoreCase);
        if(signature==lastSignature){Thumbnails.RequestVisible(scroll);return;}
        lastSignature=signature;
        tiles.Children.Clear();rows.Children.Clear();
        tiles.Visibility=tree?Visibility.Collapsed:Visibility.Visible;
        rows.Visibility=tree?Visibility.Visible:Visibility.Collapsed;
        if(tree){RenderTree(tab);}
        else{
            bool atRoot=!IsFolderTab||Session==null||String.Equals(Session.Current,Session.Root,StringComparison.OrdinalIgnoreCase);
            if(IsFolderTab&&!atRoot)tiles.Children.Add(MakeUpTile());
            foreach(string path in items)tiles.Children.Add(MakeTile(path,null));
            emptyText.Visibility=items.Length==0&&atRoot?Visibility.Visible:Visibility.Collapsed;
            if(items.Length==0&&atRoot)emptyText.Text=IsFolderTab?"此文件夹为空；可以把文件拖进来。":"这个分区还没有项目。";
        }
        string label=tab.IsFolder?FilePresentation.FolderName(folder):(CurrentGroup==null?"分区":CurrentGroup.Name);
        summary.Text=label+" · "+items.Length+" 项";
        summary.ToolTip=tab.IsFolder?folder:null;
        Thumbnails.RequestVisible(scroll);
    }
    void ShowEmpty(string message){
        tiles.Children.Clear();rows.Children.Clear();lastSignature=null;
        emptyText.Visibility=Visibility.Visible;emptyText.Text=message;summary.Text="";
    }
    string[] TabItems(PanelTab tab){
        var data=controller.Store.Data;
        if(tab.IsFolder){
            string folder=CurrentFolder;
            if(String.IsNullOrEmpty(folder)||!Directory.Exists(folder))return null;
            try{return Directory.GetFileSystemEntries(folder).Where(p=>!Path.GetFileName(p).Equals("desktop.ini",StringComparison.OrdinalIgnoreCase)).Where(p=>!PanelFiles.Ignored(data,p,tab.IgnoreRules)).ToArray();}
            catch{return null;}
        }
        var group=data.Groups.FirstOrDefault(g=>g.Id==tab.GroupId);
        if(group==null)return null;
        try{return controller.Store.Items(group,"").ToArray();}catch{return new string[0];}
    }
    void RefreshTabs(){
        if(closed)return;
        var data=controller.Store.Data;
        string signature=String.Join("|",data.PanelTabs.Select(t=>t.IsFolder?t.Folder:"#"+t.GroupId))+"#"+data.PanelIndex;
        if(signature==lastTabSignature)return;
        lastTabSignature=signature;
        tabBar.Children.Clear();
        for(int i=0;i<data.PanelTabs.Count;i++){
            int index=i;bool active=index==data.PanelIndex;var tab=data.PanelTabs[i];
            string label=tab.IsFolder?FilePresentation.FolderName(tab.Folder):(data.Groups.FirstOrDefault(g=>g.Id==tab.GroupId)==null?"分区":data.Groups.FirstOrDefault(g=>g.Id==tab.GroupId).Name);
            string tooltip=tab.IsFolder?tab.Folder:"桌面分区 · "+label;
            var text=Ui.Text(label,17,active?Ui.DesktopInk:Ui.DesktopMuted);text.Margin=new Thickness(0);text.TextTrimming=TextTrimming.CharacterEllipsis;text.VerticalAlignment=VerticalAlignment.Center;text.MaxWidth=190;
            if(active)text.FontWeight=FontWeights.SemiBold;
            var close=new TextBlock(){Text="×",FontSize=12,Foreground=Ui.DesktopMuted,Margin=new Thickness(6,0,0,0),Cursor=Cursors.Hand,VerticalAlignment=VerticalAlignment.Center,Opacity=0,IsHitTestVisible=false};
            close.MouseLeftButtonDown+=delegate(object s,MouseButtonEventArgs e){e.Handled=true;RemoveTab(index);};
            var row=new StackPanel(){Orientation=Orientation.Horizontal};row.Children.Add(text);row.Children.Add(close);
            var box=new Border(){Child=row,Cursor=Cursors.Hand,Padding=new Thickness(7,2,7,2),Margin=new Thickness(0,0,5,0),CornerRadius=new CornerRadius(0),Background=Brushes.Transparent,BorderBrush=Ui.Brush(VisualStyle.Color),BorderThickness=new Thickness(0,0,0,active?2:0),ToolTip=tooltip+"\n左键切换，中键或悬停后点 × 关闭",AllowDrop=true};
            box.MouseEnter+=delegate{close.Opacity=1;close.IsHitTestVisible=true;};
            box.MouseLeave+=delegate{close.Opacity=0;close.IsHitTestVisible=false;};
            box.MouseLeftButtonDown+=delegate(object s,MouseButtonEventArgs e){e.Handled=true;SelectTab(index);};
            box.MouseDown+=delegate(object s,MouseButtonEventArgs e){if(e.ChangedButton==MouseButton.Middle){e.Handled=true;RemoveTab(index);}};
            box.DragOver+=delegate(object s,DragEventArgs e){var paths=Functions.DragPaths(e.Data);e.Effects=paths==null?DragDropEffects.None:paths.All(Directory.Exists)?DragDropEffects.Link:DropEffect(e,tab.IsFolder?tab.Folder:"");e.Handled=true;};
            box.Drop+=delegate(object s,DragEventArgs e){e.Handled=true;var paths=Functions.DragPaths(e.Data);if(paths==null)return;if(paths.All(Directory.Exists)){AddFolders(paths);}else{SelectTab(index);DropInto(e,tab.IsFolder?tab.Folder:"");}};
            var menu=new ContextMenu();
            menu.Items.Add(Ui.Menu("切换到此标签",delegate{SelectTab(index);}));
            if(tab.IsFolder){
                menu.Items.Add(Ui.Menu("回到根目录",delegate{SelectTab(index);}));
                menu.Items.Add(Ui.Menu("在资源管理器中打开",delegate{Shell.Open(tab.Folder);}));
            }else{
                menu.Items.Add(Ui.Menu("在桌面上显示这个分区",delegate{if(controller.Manager!=null){controller.Manager.RestoreDesktopCopy(tab.GroupId);RemoveTab(index);}}));
            }
            menu.Items.Add(Ui.Menu("管理此标签的忽略规则",delegate{ignoreTarget.ContextMenu=BuildIgnoreMenu(tab);ignoreTarget.ContextMenu.PlacementTarget=box;ignoreTarget.ContextMenu.IsOpen=true;}));
            menu.Items.Add(new Separator());
            menu.Items.Add(Ui.Menu("左移一位",delegate{MoveTab(index,-1);}));
            menu.Items.Add(Ui.Menu("右移一位",delegate{MoveTab(index,1);}));
            menu.Items.Add(new Separator());
            menu.Items.Add(Ui.Menu("关闭此标签（不动磁盘文件）",delegate{RemoveTab(index);}));
            box.ContextMenu=menu;
            tabBar.Children.Add(box);
        }
        var add=new Border(){Child=new TextBlock(){Text="＋",FontSize=17,Foreground=Ui.DesktopMuted,VerticalAlignment=VerticalAlignment.Center},Padding=new Thickness(5,1,5,3),Margin=new Thickness(2,0,0,0),CornerRadius=new CornerRadius(0),BorderBrush=Brushes.Transparent,BorderThickness=new Thickness(0),Cursor=Cursors.Hand,ToolTip="选择文件夹作为新标签",Opacity=0,IsHitTestVisible=false};
        add.MouseLeftButtonDown+=delegate(object s,MouseButtonEventArgs e){e.Handled=true;ChooseFolder();};
        addTab=add;
        tabBar.Children.Add(add);
    }
    ContextMenu BuildTabBarMenu(){
        var menu=new ContextMenu();
        menu.Items.Add(Ui.Menu("新建文件夹标签…",ChooseFolder));
        menu.Items.Add(Ui.Menu("添加当前项目",delegate{string current=WorkspaceSelection.Current(controller.Store.Data);if(String.IsNullOrEmpty(current)){summary.Text="还没有连接项目；可在 AI 工作目录里添加。";return;}AddFolders(new[]{current});}));
        menu.Items.Add(new Separator());
        menu.Items.Add(Ui.Menu("刷新",delegate{lastSignature=null;lastTabSignature=null;Refresh();}));
        menu.Items.Add(Ui.Menu("关闭全部标签",delegate{var data=controller.Store.Data;data.PanelTabs.Clear();data.PanelIndex=-1;EnsureSessions();lastSignature=null;lastTabSignature=null;controller.Store.Save();Refresh();if(controller.Manager!=null)controller.Manager.RestoreDesktopCopies(null);}));
        return menu;
    }
    ContextMenu BuildBackgroundMenu(){
        var menu=new ContextMenu();
        menu.Items.Add(Ui.Menu("新建文件夹…",NewFolder));
        menu.Items.Add(Ui.Menu("粘贴",PasteClipboard));
        menu.Items.Add(new Separator());
        menu.Items.Add(Ui.Menu("在资源管理器中打开当前文件夹",delegate{string folder=CurrentFolder;if(Directory.Exists(folder))Shell.Open(folder);}));
        menu.Items.Add(Ui.Menu("在顶部新建文件夹标签…",ChooseFolder));
        menu.Items.Add(new Separator());
        var viewMenu=new MenuItem(){Header="显示方式"};
        foreach(string mode in new[]{"图标","列表","详细信息","树"}){string choice=mode;var item=Ui.Menu(choice,delegate{controller.Store.Data.PanelViewMode=choice;controller.Store.Save();lastSignature=null;Refresh();});item.IsChecked=controller.Store.Data.PanelViewMode==choice;viewMenu.Items.Add(item);}
        menu.Items.Add(viewMenu);
        var sizeMenu=new MenuItem(){Header="图标大小"};
        foreach(double size in new double[]{28,36,44,56,72,96}){double value=size;var item=Ui.Menu(((int)size).ToString()+" 像素",delegate{controller.Store.Data.PanelViewMode="图标";controller.Store.Data.PanelIconSize=value;controller.Store.Save();lastSignature=null;Refresh();});item.IsChecked=Math.Abs(controller.Store.Data.PanelIconSize-size)<.5&&controller.Store.Data.PanelViewMode=="图标";sizeMenu.Items.Add(item);}
        menu.Items.Add(sizeMenu);
        menu.Items.Add(BuildStyleMenu());
        menu.Items.Add(new Separator());
        menu.Items.Add(Ui.Menu("钉到桌面（变成分区）",PinToDesktop));
        menu.Items.Add(Ui.Menu("打开管理面板",delegate{if(controller.Manager!=null){controller.Manager.Show();controller.Manager.WindowState=WindowState.Normal;controller.Manager.Activate();}}));
        return menu;
    }
    MenuItem BuildStyleMenu(){
        var style=VisualStyle;var root=new MenuItem(){Header="面板外观"};
        var corners=new MenuItem(){Header="边角"};
        foreach(string value in new[]{"方角","圆角"}){string choice=value;var item=Ui.Menu(choice,delegate{style.CornerStyle=choice;controller.Store.Save();ApplyStyle();});item.IsChecked=style.CornerStyle==choice;corners.Items.Add(item);}
        root.Items.Add(corners);
        var background=new MenuItem(){Header="背景底色"};
        foreach(string value in Store.BackgroundStyles){string choice=value;var item=Ui.Menu(choice,delegate{style.BackgroundStyle=choice;controller.Store.Save();ApplyStyle();});item.IsChecked=style.BackgroundStyle==choice;background.Items.Add(item);}
        root.Items.Add(background);
        root.Items.Add(Ui.Menu("选择背景图片…",ChooseBackgroundImage));
        if(!String.IsNullOrEmpty(style.BackgroundImage)){
            var modes=new MenuItem(){Header="图片填充"};
            foreach(string value in Store.ImageModes){string choice=value;var item=Ui.Menu(choice,delegate{style.ImageMode=choice;controller.Store.Save();ApplyStyle();});item.IsChecked=style.ImageMode==choice;modes.Items.Add(item);}
            root.Items.Add(modes);
            root.Items.Add(Ui.Menu("清除背景图片",delegate{style.BackgroundImage="";style.BackgroundStyle="跟随颜色";controller.Store.Save();ApplyStyle();}));
        }
        var opacity=new MenuItem(){Header="背景不透明度"};
        foreach(double value in new double[]{0,.25,.5,.75,.9,1}){double choice=value;var item=Ui.Menu(((int)(value*100))+"%",delegate{style.BackgroundOpacity=choice;controller.Store.Save();ApplyStyle();});item.IsChecked=Math.Abs(style.BackgroundOpacity-value)<.02;opacity.Items.Add(item);}
        root.Items.Add(opacity);
        var border=new MenuItem(){Header="边框不透明度"};
        foreach(double value in new double[]{.25,.5,.75,1}){double choice=value;var item=Ui.Menu(((int)(value*100))+"%",delegate{style.BorderOpacity=choice;controller.Store.Save();ApplyStyle();});item.IsChecked=Math.Abs(style.BorderOpacity-value)<.02;border.Items.Add(item);}
        root.Items.Add(border);
        var skin=new MenuItem(){Header="图标皮肤"};
        foreach(string value in AppIconSkins.Choices){string choice=value;var item=Ui.Menu(choice,delegate{style.IconSkin=choice;controller.Store.Save();lastSignature=null;ApplyStyle();Refresh();});item.IsChecked=style.IconSkin==choice;skin.Items.Add(item);}
        root.Items.Add(skin);
        var follow=new MenuItem(){Header="跟随分区外观"};
        var none=Ui.Menu("不跟随（用面板自己的外观）",delegate{controller.Store.Data.PanelFollowGroupId="";controller.Store.Save();lastTabSignature=null;ApplyStyle();});none.IsChecked=String.IsNullOrEmpty(controller.Store.Data.PanelFollowGroupId);follow.Items.Add(none);
        foreach(var group in controller.Store.Data.Groups.Where(g=>String.IsNullOrEmpty(g.HostGroupId))){
            string id=group.Id;var item=Ui.Menu(group.Name,delegate{controller.Store.Data.PanelFollowGroupId=id;controller.Store.Save();lastTabSignature=null;ApplyStyle();});item.IsChecked=controller.Store.Data.PanelFollowGroupId==id;follow.Items.Add(item);
        }
        root.Items.Add(follow);
        return root;
    }
    void ChooseBackgroundImage(){
        var dialog=new Microsoft.Win32.OpenFileDialog(){Title="选择面板背景图片",Filter="图片|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp;*.tif;*.tiff"};
        if(dialog.ShowDialog(this)!=true)return;
        var style=VisualStyle;style.BackgroundImage=dialog.FileName;style.BackgroundStyle="图片";controller.Store.Save();ApplyStyle();
    }
    void ChooseFolder(){
        using(var dialog=new System.Windows.Forms.FolderBrowserDialog()){dialog.Description="选择要作为标签的文件夹";if(dialog.ShowDialog()!=System.Windows.Forms.DialogResult.OK)return;AddFolders(new[]{dialog.SelectedPath});}
    }
    // ---- 缩放：大图标 → 中 → 小 → 列表 → 可折叠树 ----
    void OnWheel(object sender,MouseWheelEventArgs e){
        if((Keyboard.Modifiers&ModifierKeys.Control)==0)return;
        e.Handled=true;
        int steps=Math.Max(1,Math.Abs(e.Delta)/120);
        for(int i=0;i<Math.Min(steps,4);i++)Zoom(Math.Sign(e.Delta));
    }
    void Zoom(int direction){
        var data=controller.Store.Data;
        int index=LadderIndex(data.PanelViewMode,data.PanelIconSize);
        int target=Math.Max(0,Math.Min(ZoomLadder.Length-1,index-direction));
        if(target==index)return;
        ApplyLevel(data,ZoomLadder[target]);controller.Store.Save();lastSignature=null;ApplyStyle();Refresh();
        summary.Text="显示方式："+ZoomLadder[target].Replace("图标:","图标 ")+"（Ctrl＋滚轮切换）";
    }
    public static int LadderIndex(string mode,double size){
        if(mode=="树")return 8;
        if(mode=="列表")return 7;
        if(mode=="详细信息")return 6;
        double[] sizes={96,72,56,44,36,28};
        for(int i=0;i<sizes.Length;i++)if(size>=sizes[i]-4)return i;
        return 5;
    }
    public static void ApplyLevel(State data,string level){
        if(level.StartsWith("图标:",StringComparison.Ordinal)){data.PanelViewMode="图标";data.PanelIconSize=Double.Parse(level.Substring(3),System.Globalization.CultureInfo.InvariantCulture);}
        else data.PanelViewMode=level;
    }
    // ---- 可折叠树 ----
    void RenderTree(PanelTab tab){
        if(tab.IsFolder){
            var session=Session;string root=session==null?"":session.Current.TrimEnd(Path.DirectorySeparatorChar);
            if(!Directory.Exists(root)){ShowEmpty("这个文件夹暂时打不开。");return;}
            if(session!=null&&!String.Equals(session.Current,session.Root,StringComparison.OrdinalIgnoreCase))rows.Children.Add(TreeUpRow());
            AddTreeRows(root,0,tab);
            return;
        }
        foreach(string path in TabItems(tab))rows.Children.Add(TreeRow(path,0,tab));
    }
    IEnumerable<string> TreeChildren(string folder){
        var data=controller.Store.Data;string[] entries;
        try{entries=Directory.GetFileSystemEntries(folder);}catch{return new string[0];}
        return entries.Where(p=>!Path.GetFileName(p).Equals("desktop.ini",StringComparison.OrdinalIgnoreCase)).Where(p=>!PanelFiles.Ignored(data,p,CurrentTab==null?null:CurrentTab.IgnoreRules)).OrderByDescending(Directory.Exists).ThenBy(Path.GetFileName,StringComparer.CurrentCultureIgnoreCase);
    }
    Border TreeUpRow(){
        var text=Ui.Text("↑ 上一层",12,Ui.DesktopInk);text.Margin=new Thickness(0);text.VerticalAlignment=VerticalAlignment.Center;
        var box=new Border(){Child=text,Padding=new Thickness(20,3,6,3),Margin=new Thickness(0,1,0,1),CornerRadius=new CornerRadius(6),Background=Brushes.Transparent,BorderThickness=new Thickness(1),BorderBrush=Brushes.Transparent,Cursor=Cursors.Hand,Tag="",AllowDrop=true};
        box.MouseLeftButtonDown+=delegate(object s,MouseButtonEventArgs e){e.Handled=true;var session=Session;if(session!=null){session.Parent();expanded.Clear();lastSignature=null;Refresh();}};
        string parent=Path.GetDirectoryName(CurrentFolder);
        box.DragOver+=delegate(object s,DragEventArgs e){e.Effects=DropEffect(e,parent);e.Handled=true;};
        box.Drop+=delegate(object s,DragEventArgs e){e.Handled=true;DropInto(e,parent);};
        return box;
    }
    void AddTreeRows(string folder,double indent,PanelTab tab){
        foreach(string path in TreeChildren(folder)){
            rows.Children.Add(TreeRow(path,indent,tab));
            if(Directory.Exists(path)&&expanded.Contains(path))AddTreeRows(path,indent+16,tab);
        }
    }
    Border TreeRow(string path,double indent,PanelTab tab){
        bool directory=Directory.Exists(path);
        bool open=directory&&expanded.Contains(path);
        var row=new StackPanel(){Orientation=Orientation.Horizontal,Margin=new Thickness(indent,0,0,0)};
        var arrow=new TextBlock(){Text=directory?(open?"▾":"▸"):"",FontSize=10,Width=14,Foreground=Ui.DesktopMuted,VerticalAlignment=VerticalAlignment.Center,Cursor=directory?Cursors.Hand:Cursors.Arrow};
        if(directory)arrow.MouseLeftButtonDown+=delegate(object s,MouseButtonEventArgs e){e.Handled=true;ToggleExpand(path);};
        row.Children.Add(arrow);
        double size=16;
        var icon=new Image(){Width=size,Height=size,Stretch=Stretch.Uniform,SnapsToDevicePixels=true,VerticalAlignment=VerticalAlignment.Center};
        RenderOptions.SetBitmapScalingMode(icon,BitmapScalingMode.HighQuality);
        icon.Source=Shell.Icon(path);Thumbnails.Bind(icon,path,size);
        row.Children.Add(icon);
        var name=new TextBlock(){Text=FilePresentation.DisplayName(path,controller.Store.Data.ShowFileExtensions),FontSize=12,Foreground=Ui.DesktopInk,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(6,0,0,0),TextTrimming=TextTrimming.CharacterEllipsis};
        row.Children.Add(name);
        if(!tab.IsFolder)row.Children.Add(new TextBlock(){Text="  "+FilePresentation.FolderName(Path.GetDirectoryName(path)),FontSize=10,Foreground=Ui.DesktopMuted,VerticalAlignment=VerticalAlignment.Center});
        var box=new Border(){Child=row,Padding=new Thickness(4,2,6,2),Margin=new Thickness(0,1,0,1),CornerRadius=new CornerRadius(6),Background=Brushes.Transparent,BorderThickness=new Thickness(1),BorderBrush=Brushes.Transparent,Cursor=Cursors.Hand,Tag=path,ToolTip=path+"\n"+FilePresentation.Detail(path),AllowDrop=directory};
        box.MouseEnter+=delegate{if(!selection.Any(p=>String.Equals(p,path,StringComparison.OrdinalIgnoreCase)))box.Background=new SolidColorBrush(Color.FromArgb(56,110,142,184));};
        box.MouseLeave+=delegate{if(!selection.Any(p=>String.Equals(p,path,StringComparison.OrdinalIgnoreCase)))box.Background=Brushes.Transparent;};
        box.MouseLeftButtonDown+=delegate(object s,MouseButtonEventArgs e){
            if(e.ClickCount==2){ClearSelection();if(directory)ToggleExpand(path);else Activate(path);e.Handled=true;return;}
            Select(path,(Keyboard.Modifiers&ModifierKeys.Control)!=0);
            box.Background=new SolidColorBrush(Color.FromArgb(70,110,142,184));
            e.Handled=true;
        };
        if(directory){
            box.DragOver+=delegate(object s,DragEventArgs e){e.Effects=DropEffect(e,path);e.Handled=true;};
            box.Drop+=delegate(object s,DragEventArgs e){e.Handled=true;DropInto(e,path);};
        }
        box.ContextMenu=BuildItemMenu(path,directory,tab);
        return box;
    }
    void ToggleExpand(string path){
        if(!expanded.Remove(path))expanded.Add(path);
        lastSignature=null;Refresh();
    }
    // ---- 文件操作 ----
    void NewFolder(){
        if(!IsFolderTab)return;
        string folder=CurrentFolder;if(!Directory.Exists(folder))return;
        string name=Ui.Ask("新建文件夹","文件夹名称","新建文件夹",this);if(String.IsNullOrWhiteSpace(name))return;
        if(name.IndexOfAny(Path.GetInvalidFileNameChars())>=0||name.EndsWith(".")||name.EndsWith(" ")){Ui.Info("请使用有效文件夹名。",this);return;}
        try{Directory.CreateDirectory(PanelFiles.Unique(folder,name));lastSignature=null;lastTabSignature=null;Refresh();}
        catch(Exception ex){Ui.Info("新建文件夹未完成："+ex.Message,this);}
    }
    static bool HasDirectory(System.Windows.IDataObject data){var paths=Functions.DragPaths(data);return paths!=null&&paths.Any(Directory.Exists);}
    DragDropEffects DropEffect(DragEventArgs e){return DropEffect(e,CurrentFolder);}
    // 与 VSCode / Cursor 侧栏一致：直接拖动是移动，按住 Ctrl 拖动是复制。
    DragDropEffects DropEffect(DragEventArgs e,string destination){
        if(!IsFolderTab)return DragDropEffects.None;
        var paths=Functions.DragPaths(e.Data);
        if(!PanelFiles.CanPasteInto(paths,destination))return DragDropEffects.None;
        bool copy=(e.KeyStates&DragDropKeyStates.ControlKey)!=0;
        DragDropEffects wanted=copy?DragDropEffects.Copy:DragDropEffects.Move;
        if((e.AllowedEffects&wanted)!=0)return wanted;
        DragDropEffects fallback=copy?DragDropEffects.Move:DragDropEffects.Copy;
        return (e.AllowedEffects&fallback)!=0?fallback:DragDropEffects.None;
    }
    void DropInto(DragEventArgs e,string destination){
        if(!IsFolderTab)return;
        var paths=Functions.DragPaths(e.Data);if(!PanelFiles.CanPasteInto(paths,destination))return;
        bool copy=(e.KeyStates&DragDropKeyStates.ControlKey)!=0;
        Transfer(paths,destination,!copy);
    }
    bool Transfer(string[] paths,string destination,bool move){
        if(!PanelFiles.CanPasteInto(paths,destination))return false;
        try{
            var data=new System.Windows.Forms.DataObject();var files=new System.Collections.Specialized.StringCollection();files.AddRange(paths);data.SetFileDropList(files);
            bool done=NativeFileOperations.Paste(new WindowInteropHelper(this).Handle,destination,(System.Runtime.InteropServices.ComTypes.IDataObject)data,move);
            if(!done)return false;
            if(move)foreach(string path in paths){string target=Path.Combine(destination,Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar)));if(Store.Exists(target))ShellActions.UpdateReferences(controller.Store.Data,path,target,Directory.Exists(target));}
            controller.SaveAndRefresh(false);lastSignature=null;Refresh();
            summary.Text=(move?"移动":"复制")+"了 "+paths.Length+" 项到 "+FilePresentation.FolderName(destination)+"。";
            return true;
        }catch(Exception ex){summary.Text=(move?"移动":"复制")+"未完成："+ex.Message;return false;}
    }
    void DeleteSelection(){
        var targets=selection.Where(Store.Exists).ToArray();if(targets.Length==0)return;
        if(MessageBox.Show(this,"将 "+targets.Length+" 个项目移入回收站？\n\n"+String.Join("\n",targets.Take(8).Select(Path.GetFileName)),"栖桌 · 删除",MessageBoxButton.OKCancel,MessageBoxImage.Warning)!=MessageBoxResult.OK)return;
        try{NativeFileOperations.Delete(new WindowInteropHelper(this).Handle,targets);}
        catch(Exception ex){Ui.Info("删除未完成："+ex.Message,this);}
        selection.Clear();lastSignature=null;Refresh();
    }
    void Rename(string path){
        if(String.IsNullOrEmpty(path)||!Store.Exists(path))return;
        string current=Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar));
        string name=Ui.Ask("重命名",Directory.Exists(path)?"文件夹名称":"文件名称",current,this);
        if(String.IsNullOrEmpty(name)||name==current)return;
        try{
            bool directory=Directory.Exists(path);
            if(ShellActions.Rename(this,path,name)){string destination=Path.Combine(Path.GetDirectoryName(path),name);ShellActions.UpdateReferences(controller.Store.Data,path,destination,directory);controller.SaveAndRefresh(false);}
        }catch(Exception ex){Ui.Info(ex.Message,this);}
        selection.Clear();lastSignature=null;Refresh();
    }
    void CopyToClipboard(bool cut){
        if(!IsFolderTab){summary.Text="桌面分区标签只用于浏览，请切到文件夹标签再操作文件。";return;}
        var targets=selection.Where(Store.Exists).ToArray();if(targets.Length==0)return;
        clipboard.Clear();clipboard.AddRange(targets);clipboardCut=cut;
        summary.Text=(cut?"已剪切 ":"已复制 ")+targets.Length+" 项；在目标文件夹按 Ctrl＋V。";
    }
    void PasteClipboard(){
        if(!IsFolderTab){summary.Text="桌面分区标签只用于浏览。";return;}
        if(clipboard.Count==0){summary.Text="剪切板是空的。";return;}
        string[] paths=clipboard.ToArray();string destination=CurrentFolder;
        if(!Transfer(paths,destination,clipboardCut))return;
        if(clipboardCut)clipboard.Clear();
        selection.Clear();lastSignature=null;Refresh();
    }
    void IgnoreDropped(string[] paths){
        if(paths==null||paths.Length==0)return;
        var rules=ActiveIgnoreRules;var pending=new List<IgnoreRule>();int skipped=0;
        try{
            foreach(string path in paths){
                if(!Store.Exists(path))continue;
                if(PanelFiles.HasExactIgnore(rules.Concat(pending),path)){skipped++;continue;}
                pending.Add(PanelFiles.MakeIgnoreRule(path));
            }
            IgnoreRule.Validate(rules.Concat(pending).ToList());
        }catch(Exception ex){summary.Text="忽略规则未添加："+ex.Message;return;}
        if(pending.Count>0){controller.Store.Checkpoint();rules.AddRange(pending);controller.Store.Save();lastSignature=null;Refresh();}
        summary.Text=pending.Count==0?"这些项目已经在此标签的忽略列表里了。":"此标签已忽略 "+pending.Count+" 项"+(skipped>0?"，跳过 "+skipped+" 项":"")+"；右键忽略图标可撤销。";
    }
    // ---- 钉到桌面：把面板变回一个分区 ----
    public void PinToDesktop(){
        var data=controller.Store.Data;
        if(data.PanelTabs.Count==0){summary.Text="面板里还没有标签。";return;}
        var folders=data.PanelTabs.Where(t=>t.IsFolder).Select(t=>t.Folder).ToArray();
        var groupIds=data.PanelTabs.Where(t=>!t.IsFolder).Select(t=>t.GroupId).ToArray();
        controller.Store.Checkpoint();
        Group host=null;
        if(folders.Length>0){
            host=data.Groups.FirstOrDefault(g=>g.Id==data.PinnedGroupId&&String.IsNullOrEmpty(g.HostGroupId));
            if(host==null){host=new Group(){Name=FilePresentation.FolderName(folders[0]),Kind="文件夹"};data.Groups.Add(host);}
            host.Name=FilePresentation.FolderName(folders[0]);host.Kind="文件夹";host.Folders=folders.ToList();host.FolderIndex=Math.Max(0,Math.Min(folders.Length-1,IndexOfFolderTab(data,folders)));
            host.Folder=folders[0];
            host.FolderIgnoreRules=data.PanelTabs.Where(t=>t.IsFolder).ToDictionary(t=>t.Folder,t=>t.IgnoreRules.ToList(),StringComparer.OrdinalIgnoreCase);
            host.X=Left;host.Y=Top;host.Width=Width;host.Height=Height;host.Page=data.Page;host.CornerStyle=VisualStyle.CornerStyle;
            host.Color=VisualStyle.Color;host.TintStrength=VisualStyle.TintStrength;host.BackgroundOpacity=VisualStyle.BackgroundOpacity;host.BorderOpacity=VisualStyle.BorderOpacity;
            host.BackgroundStyle=VisualStyle.BackgroundStyle;host.BackgroundImage=VisualStyle.BackgroundImage;host.ImageMode=VisualStyle.ImageMode;
            host.IconSkin=VisualStyle.IconSkin;host.IconColor=VisualStyle.IconColor;host.LabelColor=VisualStyle.LabelColor;host.LabelSize=VisualStyle.LabelSize;host.LabelFont=VisualStyle.LabelFont;host.IconSize=VisualStyle.IconSize;
        }
        foreach(string id in groupIds){
            var member=data.Groups.FirstOrDefault(g=>g.Id==id);if(member==null)continue;
            member.HostGroupId="";member.Page=data.Page;
            if(host!=null){member.HostGroupId=host.Id;member.Page=host.Page;host.ActiveGroupId=member.Id;}
            else{moveOutOfPanel(member);}
        }
        data.PinnedGroupId=host==null?"":host.Id;
        data.PanelTabs.Clear();data.PanelIndex=-1;EnsureSessions();
        controller.Store.Save();
        if(controller.Manager!=null)controller.Manager.RestoreDesktopCopies(groupIds);
        Dismiss();
        controller.SaveAndRefresh();
        if(controller.Manager!=null)controller.Manager.SetStatus(host!=null?"工作区面板已钉到桌面，变成一个分区；可以用小猫或 Alt＋Space 再把它弹成面板。":"工作区面板的分区标签已回到桌面。");
    }
    void moveOutOfPanel(Group member){
        var data=controller.Store.Data;
        if(member.X<0||member.Y<0){member.X=Left+36;member.Y=Top+48;}
        member.Page=data.Page;
    }
    static int IndexOfFolderTab(State data,string[] folders){
        for(int i=0;i<data.PanelTabs.Count;i++)if(data.PanelTabs[i].IsFolder&&String.Equals(data.PanelTabs[i].Folder,folders[0],StringComparison.OrdinalIgnoreCase))return i;
        return 0;
    }
    // ---- 搜索：只按名称递归匹配，不使用任何向量或语义模型 ----
    void ToggleSearch(){if(searchBar.Visibility==Visibility.Visible)CloseSearch();else OpenSearch();}
    void OpenSearch(){searchBar.Visibility=Visibility.Visible;searchBox.Focus();Keyboard.Focus(searchBox);}
    void CloseSearch(){
        searchDebounce.Stop();generation++;
        if(searchBar.Visibility!=Visibility.Visible)return;
        searchBar.Visibility=Visibility.Collapsed;
        suppressSearch=true;searchBox.Clear();suppressSearch=false;
        lastSignature=null;Refresh();
    }
    void ScheduleSearch(){
        if(searchBar.Visibility!=Visibility.Visible)return;
        generation++;searchDebounce.Stop();
        if(searchBox.Text.Length==0){lastSignature=null;Refresh();return;}
        summary.Text="正在查找…";searchDebounce.Start();
    }
    async void RunSearch(){
        if(closed)return;
        string query=searchBox.Text.Trim();
        if(query.Length==0){lastSignature=null;Refresh();return;}
        int token=generation;
        State snapshot=Store.Json.Deserialize<State>(Store.Json.Serialize(controller.Store.Data));
        var tab=CurrentTab;string root=CurrentFolder;
        try{
            string[] found;
            if(tab!=null&&!tab.IsFolder){
                var group=CurrentGroup;
                found=group==null?new string[0]:controller.Store.Items(group,"").Where(p=>Path.GetFileName(p).IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0).Take(PanelFiles.SearchLimit).ToArray();
            }else{
                if(!Directory.Exists(root))return;
                var scoped=tab==null?null:tab.IgnoreRules.ToList();
                found=await Task.Run(()=>PanelFiles.Search(snapshot,root,query,scoped));
            }
            if(closed||token!=generation||searchBar.Visibility!=Visibility.Visible)return;
            RenderResults(found,root,query);
        }catch(Exception ex){summary.Text="搜索未完成："+ex.Message;}
    }
    void RenderResults(string[] found,string root,string query){
        tiles.Children.Clear();rows.Children.Clear();lastSignature=null;
        rows.Visibility=Visibility.Collapsed;tiles.Visibility=Visibility.Visible;
        emptyText.Visibility=found.Length==0?Visibility.Visible:Visibility.Collapsed;
        if(found.Length==0)emptyText.Text="没有名称包含「"+query+"」的文件或文件夹。";
        foreach(string path in found.Take(PanelFiles.SearchLimit))tiles.Children.Add(MakeTile(path,IsFolderTab?PanelFiles.Relative(root,path):FilePresentation.FolderName(Path.GetDirectoryName(path))));
        summary.Text="名称搜索「"+query+"」· "+found.Length+" 项"+(found.Length>=PanelFiles.SearchLimit?"（已达 "+PanelFiles.SearchLimit+" 项上限）":"")+" · Enter 打开，Esc 收起搜索";
        summary.ToolTip=null;
        Thumbnails.RequestVisible(scroll);
    }
    // ---- 选择与图标网格 ----
    void ClearSelection(){
        if(selection.Count==0)return;
        selection.Clear();
        foreach(var box in ItemBoxes()){box.BorderBrush=Brushes.Transparent;box.Background=Brushes.Transparent;}
    }
    IEnumerable<Border> ItemBoxes(){return tiles.Children.OfType<Border>().Concat(rows.Children.OfType<Border>());}
    void Select(string path,bool toggle){
        var box=ItemBoxes().FirstOrDefault(b=>String.Equals(Convert.ToString(b.Tag),path,StringComparison.OrdinalIgnoreCase));
        if(!toggle)ClearSelection();
        if(selection.Any(p=>String.Equals(p,path,StringComparison.OrdinalIgnoreCase))){selection.RemoveAll(p=>String.Equals(p,path,StringComparison.OrdinalIgnoreCase));if(box!=null)box.BorderBrush=Brushes.Transparent;return;}
        selection.Add(path);if(box!=null)box.BorderBrush=Ui.Accent;
    }
    Border MakeUpTile(){
        string parent=Path.GetDirectoryName(CurrentFolder);
        var label=Ui.Text("↑ 上一层",12,Ui.DesktopInk);label.Margin=new Thickness(0);label.TextAlignment=TextAlignment.Center;label.VerticalAlignment=VerticalAlignment.Center;
        var box=new Border(){Width=Math.Max(96,controller.Store.Data.PanelIconSize+40),Height=64,Margin=new Thickness(3),CornerRadius=new CornerRadius(VisualStyle.CornerStyle=="方角"?6:10),Background=new SolidColorBrush(Color.FromArgb(60,120,160,210)),BorderThickness=new Thickness(1),BorderBrush=Brushes.Transparent,Cursor=Cursors.Hand,AllowDrop=true,ToolTip="回到上一层"};
        box.Child=label;box.Tag="";
        box.MouseLeftButtonDown+=delegate(object s,MouseButtonEventArgs e){e.Handled=true;var session=Session;if(session!=null){session.Parent();expanded.Clear();lastSignature=null;Refresh();}};
        box.DragOver+=delegate(object s,DragEventArgs e){e.Effects=DropEffect(e,parent);e.Handled=true;};
        box.Drop+=delegate(object s,DragEventArgs e){e.Handled=true;DropInto(e,parent);};
        return box;
    }
    Border MakeTile(string path,string subtitle){
        var data=controller.Store.Data;var style=VisualStyle;
        bool listMode=data.PanelViewMode!="图标";
        bool detailMode=data.PanelViewMode=="详细信息";
        bool directory=Directory.Exists(path);
        double size=listMode?24:data.PanelIconSize;
        Brush plateBackground=style.IconSkin=="Fluent 线条"?new SolidColorBrush(Color.FromArgb(225,236,243,250)):style.IconSkin=="Fluent 填充"?new SolidColorBrush(Color.FromArgb(240,28,40,60)):(Brush)(directory?new LinearGradientBrush(Color.FromArgb(235,58,78,104),Color.FromArgb(245,33,47,68),90):new LinearGradientBrush(Color.FromArgb(235,36,54,78),Color.FromArgb(245,19,32,50),90));
        Color accent=TimedAccent(style.IconColor);
        bool picture=Thumbnails.IsPicture(path);
        var themed=FileIconPresentation.Themed(path,style,data,size);
        var icon=new Image(){Width=directory?size*.88:size*.84,Height=directory?size*.88:size*.84,Stretch=Stretch.Uniform,SnapsToDevicePixels=true};
        RenderOptions.SetBitmapScalingMode(icon,BitmapScalingMode.HighQuality);
        FileIconPresentation.Bind(icon,path,size,themed);
        double plateSize=size+(listMode?6:10);
        double radius=style.CornerStyle=="方角"?0:directory?Math.Max(7,plateSize*.27):Math.Max(5,plateSize*.18);
        var plate=new Border(){Width=plateSize,Height=plateSize,CornerRadius=new CornerRadius(radius),BorderThickness=new Thickness(style.IconSkin=="Fluent 线条"?1.5:1),BorderBrush=style.IconSkin=="经典"?(picture?Ui.Brush("#D6E2EE"):directory?Ui.Brush(style.Color):Ui.Brush("#8296AD")):new SolidColorBrush(accent),Background=plateBackground,Child=icon,Margin=new Thickness(0,0,listMode?8:0,listMode?0:7),Effect=Ui.Shadow(1,5,.2)};
        if(AppIconSkins.IsPack(style.IconSkin)){plate.Background=Brushes.Transparent;plate.BorderThickness=new Thickness(0);plate.Effect=null;icon.Width=size;icon.Height=size;}
        if(picture)icon.Clip=new RectangleGeometry(new Rect(0,0,size,size),style.CornerStyle=="方角"?0:Math.Max(4,size*.16),style.CornerStyle=="方角"?0:Math.Max(4,size*.16));
        var name=new TextBlock(){Text=FilePresentation.DisplayName(path,data.ShowFileExtensions),Foreground=Ui.DesktopInk,FontSize=11,TextAlignment=listMode?TextAlignment.Left:TextAlignment.Center,TextWrapping=listMode?TextWrapping.NoWrap:TextWrapping.Wrap,MaxHeight=34,TextTrimming=TextTrimming.CharacterEllipsis,Effect=Ui.Shadow(1,3,.8)};
        Border box;
        double radiusBox=style.CornerStyle=="方角"?0:8;
        if(listMode){
            var row=new DockPanel(){Margin=new Thickness(7,4,7,4)};DockPanel.SetDock(plate,Dock.Left);row.Children.Add(plate);
            var text=new StackPanel(){VerticalAlignment=VerticalAlignment.Center};text.Children.Add(name);
            if(detailMode)text.Children.Add(new TextBlock(){Text=FilePresentation.Detail(path),FontSize=10,Foreground=Ui.DesktopMuted,TextTrimming=TextTrimming.CharacterEllipsis});
            else if(subtitle!=null)text.Children.Add(new TextBlock(){Text=subtitle,FontSize=10,Foreground=Ui.DesktopMuted,TextTrimming=TextTrimming.CharacterEllipsis});
            row.Children.Add(text);
            box=new Border(){Width=Math.Max(200,Width-40),Height=detailMode?54:38,Margin=new Thickness(3),CornerRadius=new CornerRadius(radiusBox),Background=Brushes.Transparent,BorderThickness=new Thickness(1),BorderBrush=Brushes.Transparent,Cursor=Cursors.Hand,Child=row,Tag=path,ToolTip=path+"\n"+FilePresentation.Detail(path)};
        }else{
            var stack=new StackPanel(){Margin=new Thickness(4,8,4,0)};
            stack.Children.Add(plate);stack.Children.Add(name);
            if(subtitle!=null)stack.Children.Add(new TextBlock(){Text=subtitle,FontSize=9.5,Foreground=Ui.DesktopMuted,TextAlignment=TextAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis,Margin=new Thickness(0,2,0,0)});
            box=new Border(){Width=Math.Max(88,size+34),Height=size+61+(subtitle==null?0:14),Margin=new Thickness(3),CornerRadius=new CornerRadius(radiusBox),Background=Brushes.Transparent,BorderThickness=new Thickness(1),BorderBrush=Brushes.Transparent,Cursor=Cursors.Hand,Child=stack,Tag=path,ToolTip=path+"\n"+FilePresentation.Detail(path)};
        }
        Point press=new Point();bool held=false;
        box.MouseEnter+=delegate{if(!selection.Any(p=>String.Equals(p,path,StringComparison.OrdinalIgnoreCase)))box.Background=new SolidColorBrush(Color.FromArgb(56,110,142,184));};
        box.MouseLeave+=delegate{if(!selection.Any(p=>String.Equals(p,path,StringComparison.OrdinalIgnoreCase)))box.Background=Brushes.Transparent;};
        box.MouseLeftButtonDown+=delegate(object s,MouseButtonEventArgs e){
            if(e.ClickCount==2){held=false;box.ReleaseMouseCapture();ClearSelection();Activate(path);e.Handled=true;return;}
            Select(path,(Keyboard.Modifiers&ModifierKeys.Control)!=0);
            box.Background=new SolidColorBrush(Color.FromArgb(70,110,142,184));
            press=e.GetPosition(box);held=true;box.CaptureMouse();e.Handled=true;
        };
        box.MouseLeftButtonUp+=delegate{held=false;box.ReleaseMouseCapture();};
        box.MouseMove+=delegate(object s,MouseEventArgs e){
            if(!held||e.LeftButton!=MouseButtonState.Pressed)return;
            Point point=e.GetPosition(box);if(Math.Abs(point.X-press.X)+Math.Abs(point.Y-press.Y)<10)return;
            held=false;box.ReleaseMouseCapture();
            var payload=new DataObject(DataFormats.FileDrop,new[]{path});
            bool cancelled=false;
            QueryContinueDragEventHandler cancellation=delegate(object sender,QueryContinueDragEventArgs args){if(args.EscapePressed)cancelled=true;};
            box.QueryContinueDrag+=cancellation;
            try{DragDrop.DoDragDrop(box,payload,DragDropEffects.Move|DragDropEffects.Copy|DragDropEffects.Link);}
            finally{box.QueryContinueDrag-=cancellation;}
            lastSignature=null;
            if(cancelled)summary.Text="已取消拖动。";
            controller.SaveAndRefresh(false);Refresh();
        };
        box.ContextMenu=BuildItemMenu(path,directory,CurrentTab);
        if(directory&&IsFolderTab){
            box.AllowDrop=true;
            box.DragOver+=delegate(object s,DragEventArgs e){e.Effects=DropEffect(e,path);e.Handled=true;};
            box.Drop+=delegate(object s,DragEventArgs e){e.Handled=true;DropInto(e,path);};
        }
        return box;
    }
    ContextMenu BuildItemMenu(string path,bool directory,PanelTab tab){
        bool files=tab!=null&&tab.IsFolder;
        var menu=new ContextMenu();
        menu.Items.Add(Ui.Menu(directory?"打开文件夹":"打开",delegate{Activate(path);}));
        menu.Items.Add(Ui.Menu("在资源管理器中查看",delegate{Shell.Reveal(path);}));
        menu.Items.Add(Ui.Menu("复制路径",delegate{Ui.CopyText(path);}));
        if(files){
            menu.Items.Add(new Separator());
            menu.Items.Add(Ui.Menu("重命名…",delegate{Rename(path);}));
            menu.Items.Add(Ui.Menu("复制",delegate{if(!selection.Any(p=>String.Equals(p,path,StringComparison.OrdinalIgnoreCase)))Select(path,false);CopyToClipboard(false);}));
            menu.Items.Add(Ui.Menu("剪切",delegate{if(!selection.Any(p=>String.Equals(p,path,StringComparison.OrdinalIgnoreCase)))Select(path,false);CopyToClipboard(true);}));
            if(directory){
                menu.Items.Add(Ui.Menu("粘贴到此文件夹",delegate{if(clipboard.Count==0){summary.Text="剪切板是空的。";return;}Transfer(clipboard.ToArray(),path,clipboardCut);if(clipboardCut)clipboard.Clear();lastSignature=null;Refresh();}));
                var copyMenu=new MenuItem(){Header="复制到标签"};
                var moveMenu=new MenuItem(){Header="移动到标签"};
                foreach(var target in controller.Store.Data.PanelTabs.Where(t=>t.IsFolder).Where(t=>!String.Equals(IgnoreRule.Canonical(t.Folder),IgnoreRule.Canonical(CurrentFolder),StringComparison.OrdinalIgnoreCase)).Where(t=>Directory.Exists(t.Folder))){
                    string destination=target.Folder;
                    copyMenu.Items.Add(Ui.Menu(FilePresentation.FolderName(destination),delegate{Transfer(new[]{path},destination,false);}));
                    moveMenu.Items.Add(Ui.Menu(FilePresentation.FolderName(destination),delegate{Transfer(new[]{path},destination,true);}));
                }
                if(copyMenu.Items.Count>0){menu.Items.Add(copyMenu);menu.Items.Add(moveMenu);}
            }
            menu.Items.Add(new Separator());
            menu.Items.Add(Ui.Menu("忽略此项目",delegate{IgnoreDropped(new[]{path});}));
            if(ActiveIgnoreRules.Any(r=>r.Matches(path)))menu.Items.Add(Ui.Menu("取消此标签的忽略",delegate{ActiveIgnoreRules.RemoveAll(r=>r.Matches(path));controller.Store.Save();lastSignature=null;Refresh();}));
            menu.Items.Add(Ui.Menu("删除（移入回收站）",delegate{if(!selection.Any(p=>String.Equals(p,path,StringComparison.OrdinalIgnoreCase)))Select(path,false);DeleteSelection();}));
        }else{
            menu.Items.Add(new Separator());
            menu.Items.Add(Ui.Menu("显示进度或分组…",delegate{if(controller.Manager!=null)controller.Manager.Show();}));
            menu.Items.Add(Ui.Menu("移出所在分区",delegate{var group=CurrentGroup;if(group==null||controller.Manager==null)return;controller.MoveOut(path,group);lastSignature=null;Refresh();}));
        }
        return menu;
    }
    void Activate(string path){
        if(Directory.Exists(path)){
            var session=Session;
            if(IsFolderTab&&session!=null&&session.Navigate(path)){expanded.Clear();lastSignature=null;CloseSearch();Refresh();return;}
        }
        Shell.Open(path);
    }
    // ---- 键盘 ----
    void OnKey(object sender,KeyEventArgs e){
        if(searchBox.IsKeyboardFocusWithin)return;
        bool control=(Keyboard.Modifiers&ModifierKeys.Control)!=0;
        if(e.Key==Key.Escape){if(searchBar.Visibility==Visibility.Visible)CloseSearch();else Dismiss();e.Handled=true;return;}
        if(control&&e.Key==Key.F){ToggleSearch();e.Handled=true;return;}
        if(control&&e.Key==Key.C){CopyToClipboard(false);e.Handled=true;return;}
        if(control&&e.Key==Key.X){CopyToClipboard(true);e.Handled=true;return;}
        if(control&&e.Key==Key.V){PasteClipboard();e.Handled=true;return;}
        if(control&&e.Key==Key.A){ClearSelection();foreach(var box in ItemBoxes()){string path=Convert.ToString(box.Tag);if(String.IsNullOrEmpty(path)||!Store.Exists(path))continue;selection.Add(path);box.BorderBrush=Ui.Accent;}e.Handled=true;return;}
        if(control&&(Keyboard.Modifiers&ModifierKeys.Shift)!=0&&e.Key==Key.N){NewFolder();e.Handled=true;return;}
        if(e.Key==Key.F5){lastSignature=null;lastTabSignature=null;Refresh();e.Handled=true;return;}
        if(e.Key==Key.F2){if(selection.Count==1)Rename(selection[0]);else summary.Text="先用 Ctrl＋点击选中一个项目，再按 F2 重命名。";e.Handled=true;return;}
        if(e.Key==Key.Delete){DeleteSelection();e.Handled=true;return;}
        if(e.Key==Key.Back){
            if(IsFolderTab){var session=Session;if(session!=null&&!String.Equals(session.Current,session.Root,StringComparison.OrdinalIgnoreCase)){session.Parent();expanded.Clear();lastSignature=null;Refresh();}}
            e.Handled=true;return;
        }
        if(control&&(e.Key==Key.OemPlus||e.Key==Key.Add)){Zoom(1);e.Handled=true;return;}
        if(control&&(e.Key==Key.OemMinus||e.Key==Key.Subtract)){Zoom(-1);e.Handled=true;return;}
        if(e.Key==Key.Enter){
            if(searchBar.Visibility==Visibility.Visible){var first=ItemBoxes().Select(b=>Convert.ToString(b.Tag)).FirstOrDefault(p=>!String.IsNullOrEmpty(p)&&Store.Exists(p));if(first!=null)Activate(first);}
            else if(selection.Count==1)Activate(selection[0]);
            e.Handled=true;
        }
        if(e.Key==Key.Right&&selection.Count==1&&Directory.Exists(selection[0])&&!expanded.Contains(selection[0])){expanded.Add(selection[0]);lastSignature=null;Refresh();e.Handled=true;return;}
        if(e.Key==Key.Left&&selection.Count==1&&expanded.Contains(selection[0])){expanded.Remove(selection[0]);lastSignature=null;Refresh();e.Handled=true;}
    }
    // ---- 供测试与预览使用 ----
    public TextBox SearchInput{get{return searchBox;}}
    public string Summary{get{return summary.Text;}}
    public bool SearchBarVisible{get{return searchBar.Visibility==Visibility.Visible;}}
    public string[] VisiblePaths{get{return ItemBoxes().Select(b=>Convert.ToString(b.Tag)).Where(p=>!String.IsNullOrEmpty(p)).ToArray();}}
    public string[] VisibleNames{get{return ItemBoxes().Select(b=>Convert.ToString(b.Tag)).Where(p=>!String.IsNullOrEmpty(p)).Select(p=>Path.GetFileName(p.TrimEnd(Path.DirectorySeparatorChar))).ToArray();}}
    public bool TreeVisible{get{return rows.Visibility==Visibility.Visible;}}
    public void ToggleSearchBar(){ToggleSearch();}
    public void RefreshNow(){lastSignature=null;lastTabSignature=null;ApplyStyle();Refresh();}
    public void ZoomBy(int direction){Zoom(direction);}
    public void ExpandRow(string path){if(!expanded.Contains(path))ToggleExpand(path);}
}
}
