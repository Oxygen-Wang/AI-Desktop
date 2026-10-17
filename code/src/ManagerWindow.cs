using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Interop;
using Microsoft.Win32;

namespace QiDesk {
public partial class ManagerWindow : Window {
    readonly Controller controller;
    WrapPanel cards=new WrapPanel();
    TextBlock status,stats,page;
    TextBox search;
    CheckBox doubleClick;
    System.Windows.Forms.NotifyIcon tray;
    DesktopShellActions shellActions;
    WorkspacePanel workspacePanel;
    DesktopPet pet;
    HwndSource hwnd;
    bool quitting;
    string lastCards="";string lastWorkCards="";
    public ManagerWindow(Controller c){
        controller=c;c.Manager=this;Title="栖桌 · AI Desktop";Width=1120;Height=760;MinWidth=880;MinHeight=560;
        Background=Ui.Base;Foreground=Ui.Ink;FontFamily=new FontFamily("Microsoft YaHei UI");WindowStartupLocation=WindowStartupLocation.CenterScreen;
        Icon=AppIcon.Image;InitializeShell();
        Closing+=delegate(object s,System.ComponentModel.CancelEventArgs e){if(!quitting&&tray!=null){e.Cancel=true;Hide();}};
        PreviewKeyDown+=delegate(object s,System.Windows.Input.KeyEventArgs e){var focus=System.Windows.Input.Keyboard.FocusedElement;if(focus is TextBox||focus is ComboBox)return;var modifiers=System.Windows.Input.Keyboard.Modifiers;if(modifiers!=System.Windows.Input.ModifierKeys.Control)return;if(e.Key==System.Windows.Input.Key.Left||e.Key==System.Windows.Input.Key.Right){controller.Page(e.Key==System.Windows.Input.Key.Left?-1:1);e.Handled=true;}};
        Closed+=delegate{CloseQuickSearch();CloseTutorial();CloseWorkspaceSearch();if(workspacePanel!=null){workspacePanel.Close();workspacePanel=null;}if(pet!=null){pet.Close();pet=null;}if(shellActions!=null)shellActions.Dispose();controller.CloseMcp();controller.CloseProjectDetection();controller.Stop();if(hwnd!=null){Shell.UnregisterHotKey(hwnd.Handle,1);Shell.UnregisterHotKey(hwnd.Handle,2);Shell.UnregisterHotKey(hwnd.Handle,3);Shell.UnregisterHotKey(hwnd.Handle,5);Shell.UnregisterHotKey(hwnd.Handle,6);}if(tray!=null)tray.Dispose();};
        Closed+=delegate{if(hwnd!=null)Shell.UnregisterHotKey(hwnd.Handle,7);};
        Refresh();
    }
    public void EnableTrayAndHotkeys(){
        controller.MigrateWorkspaceTabs();
        try{shellActions=new DesktopShellActions(controller);DesktopShellActions.Register(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName);}catch(Exception ex){SetStatus("桌面右键扩展未启用："+ex.Message);}
        hwnd=HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);hwnd.AddHook(Hook);bool workspaceKey=Shell.RegisterHotKey(hwnd.Handle,5,1|0x4000,0x20);if(!workspaceKey)SetStatus("Alt＋Space 已被占用，可从托盘打开工作区。");
        Shell.RegisterHotKey(hwnd.Handle,7,3|0x4000,0x08);UpdateQuickSearchHotkey();
        FileActivity.Initialize(controller.Store.Root);bool hide=Shell.RegisterHotKey(hwnd.Handle,1,3|0x4000,0x44);bool pagePrev=Shell.RegisterHotKey(hwnd.Handle,2,3|0x4000,0x25);bool pageNext=Shell.RegisterHotKey(hwnd.Handle,3,3|0x4000,0x27);bool petKey=Shell.RegisterHotKey(hwnd.Handle,6,3|0x4000,0x4D);
        if(!hide)SetStatus("Ctrl+Alt+D 已被其他程序占用，可使用托盘菜单隐藏/显示。");
        else if(!pagePrev||!pageNext)SetStatus("Ctrl+Alt+方向键已被其他程序占用，无法切换页面。");
        else if(!petKey)SetStatus("Ctrl＋Alt＋M 已被其他程序占用，可从右键菜单或托盘隐藏小猫。");
        tray=new System.Windows.Forms.NotifyIcon(){Icon=System.Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location),Text="栖桌 · AI Desktop",Visible=true};
        var menu=new System.Windows.Forms.ContextMenuStrip();menu.Items.Add("打开管理面板",null,delegate{Show();WindowState=WindowState.Normal;Activate();});menu.Items.Add("隐藏 / 显示分组",null,delegate{controller.ToggleHidden();});menu.Items.Add("工作区面板 · Alt＋Space",null,delegate{ShowWorkspacePanel(false);pet_Touch();});menu.Items.Add("工作区搜索",null,delegate{ShowWorkspaceSearch();});menu.Items.Add("搜索全盘文件",null,delegate{ShowQuickSearch();});menu.Items.Add("显示 / 隐藏小猫",null,delegate{TogglePet();});menu.Items.Add("添加桌面组件",null,delegate{ShowComponentPicker();});menu.Items.Add("使用教程",null,delegate{ShowTutorial();});menu.Items.Add("完全停止并恢复 Windows 桌面",null,delegate{Quit();});tray.ContextMenuStrip=menu;tray.DoubleClick+=delegate{Show();Activate();};
        menu.Items.RemoveAt(2);
        menu.Items.Insert(2,new System.Windows.Forms.ToolStripMenuItem("工作区 · Alt＋Space",null,delegate{ToggleWorkspaceHotkey();pet_Touch();}));
        menu.Items.Insert(3,new System.Windows.Forms.ToolStripMenuItem("取消所有工作区选择 · Ctrl＋Alt＋Backspace",null,delegate{controller.ClearGroupSelection();}));
        UpdatePet();
    }
    void pet_Touch(){if(pet!=null)pet.Touch();}
    public bool WorkspacePanelVisible{get{return workspacePanel!=null&&workspacePanel.IsVisible;}}
    // 工作区面板：Alt＋Space、小猫点击或悬停都会走到这里；toggle 为 true 时再按一次收起。
    public WorkspacePanel ShowWorkspacePanel(bool toggle){return ShowWorkspacePanel(toggle,true);}
    public WorkspacePanel ShowWorkspacePanel(bool toggle,bool focus){
        if(workspacePanel==null){var panel=new WorkspacePanel(controller);workspacePanel=panel;panel.Closed+=delegate{if(workspacePanel==panel)workspacePanel=null;};}
        if(toggle&&workspacePanel.IsVisible){workspacePanel.Dismiss();return workspacePanel;}
        workspacePanel.Present(false,focus);pet_Touch();if(pet!=null)pet.NotifyPanelVisibility(workspacePanel.IsVisible);return workspacePanel;
    }
    public void NotifyPanelVisibility(bool visible){if(pet!=null)pet.NotifyPanelVisibility(visible);}
    public void OpenWorkspaceWithFolders(string[] folders){
        if(folders==null)return;
        controller.Store.Checkpoint();
        foreach(string folder in folders.Where(System.IO.Directory.Exists).Take(60)){
            string path=System.IO.Path.GetFullPath(folder);
            var group=controller.Store.Data.Groups.FirstOrDefault(g=>String.Equals(IgnoreRule.Canonical(g.Folder),IgnoreRule.Canonical(path),StringComparison.OrdinalIgnoreCase));
            if(group==null){
                group=new Group(){Name=FilePresentation.FolderName(path),Kind="文件夹",Folder=path,Page=controller.Store.Data.Page,X=80,Y=80,Width=560,Height=400};
                group.Folders.Add(path);controller.Store.Data.Groups.Add(group);
            }
            controller.SelectedGroups.Add(String.IsNullOrEmpty(group.HostGroupId)?group.Id:group.HostGroupId);
        }
        controller.Store.Data.SelectedWorkspaceGroups=controller.SelectedGroups.ToList();
        controller.SaveAndRefresh();
        if(!controller.Running)controller.Start(true);
        controller.ToggleSelectedWorkspace();
    }
    public void ShowWorkspacePanelWithGroups(string[] groupIds){
        if(groupIds==null||groupIds.Length==0)return;
        foreach(string id in groupIds){var group=controller.Store.Data.Groups.FirstOrDefault(g=>g.Id==id);if(group!=null&&!controller.SelectedGroups.Contains(id))controller.ToggleGroupSelection(group);}
        controller.ToggleSelectedWorkspace();
    }
    // 面板显示某个分区时，先把桌面上那一份藏起来，避免同一个分区出现两次。
    readonly HashSet<string> panelHiddenGroups=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public void HideDesktopCopies(string[] groupIds){
        if(groupIds==null)return;
        bool changed=false;
        foreach(string id in groupIds){
            if(String.IsNullOrEmpty(id)||panelHiddenGroups.Contains(id))continue;
            var window=controller.Windows.FirstOrDefault(w=>w.Group.Id==id);
            if(window==null||!window.IsVisible)continue;
            panelHiddenGroups.Add(id);window.Hide();changed=true;
        }
        if(changed)SetStatus("已把 "+panelHiddenGroups.Count+" 个分区接进工作区面板；收起面板后它们会回到桌面。");
    }
    public void RestoreDesktopCopy(string groupId){
        if(String.IsNullOrEmpty(groupId)||!panelHiddenGroups.Remove(groupId))return;
        var window=controller.Windows.FirstOrDefault(w=>w.Group.Id==groupId);
        if(window==null||controller.Hidden)return;
        try{window.Show();}catch{}
    }
    // 面板显示期间接管的桌面分区：置顶时应保持面板显示，不要在这里再复制一份桌面窗口。
    public bool PanelHoldsDesktopCopy(string groupId){
        return !String.IsNullOrEmpty(groupId)&&panelHiddenGroups.Contains(groupId)&&workspacePanel!=null&&workspacePanel.IsVisible;
    }
    public void RestoreDesktopCopies(string[] groupIds){
        if(groupIds==null){foreach(string id in panelHiddenGroups.ToArray())RestoreDesktopCopy(id);return;}
        foreach(string id in groupIds)RestoreDesktopCopy(id);
    }
    public void TogglePet(){var data=controller.Store.Data;data.PetVisible=!data.PetVisible;controller.Store.Save();UpdatePet();SetStatus(data.PetVisible?"小猫已回到桌面。":"小猫已隐藏；Ctrl＋Alt＋M 或托盘菜单可以恢复。");}
    public void UpdatePet(){
        if(pet==null){
            if(!controller.Store.Data.PetVisible)return;
            pet=new DesktopPet(controller);var created=pet;pet.Closed+=delegate{if(pet==created)pet=null;};pet.Show();return;
        }
        if(controller.Store.Data.PetVisible)pet.Show();else pet.Hide();
    }
    // Alt＋Space、托盘菜单和小猫点击共用同一个入口：选中的分区在前台与桌面层之间切换，始终保持可见。
    public void ToggleWorkspaceFromPet(){ToggleWorkspaceHotkey();}
    public bool DesktopWorkspaceVisible{get{return controller.WorkspaceVisible;}}
    public int SelectedWorkspaceCount{get{return controller.SelectedGroups.Count;}}
    void ToggleWorkspaceHotkey(){
        if(WorkspacePanelVisible)workspacePanel.Dismiss();
        bool migrated=controller.MigrateWorkspaceTabs();
        if(!controller.Running){SetStatus("请先启动桌面模式，再选择需要唤起的分区。");return;}
        if(migrated)controller.Rebuild();
        controller.ToggleSelectedWorkspace();
    }
    IntPtr Hook(IntPtr h,int msg,IntPtr w,IntPtr l,ref bool handled){if(msg==0x312){handled=true;switch(w.ToInt32()){case 1:controller.ToggleHidden();break;case 2:controller.Page(-1);break;case 3:controller.Page(1);break;case 5:ToggleWorkspaceHotkey();break;case 6:TogglePet();break;case 7:controller.ClearGroupSelection();break;case 8:ShowQuickSearch(true);break;}}return IntPtr.Zero;}
    public void Quit(){quitting=true;AiClient.Shutdown();controller.Stop();try{controller.Store.Save();}catch{}if(shellActions!=null)try{DesktopShellActions.Unregister();}catch(Exception ex){SetStatus("桌面右键入口清理未完成："+ex.Message);}Close();}
    public void SetStatus(string text){status.Text=text;}
    public void Refresh(){
        var data=controller.Store.Data;stats.Text=data.Groups.Count+" 个分组 · "+data.Widgets.Count+" 个组件 · "+data.Groups.Sum(g=>controller.Store.Items(g,controller.Search).Count())+" 个项目 · "+(controller.Running?(controller.Docked?"桌面模式":"窗口预览"):"等待启动")+(controller.Hidden?" · 已隐藏":"");page.Text="第 "+(data.Page+1)+" / "+data.Pages+" 页";
        if(doubleClick!=null)doubleClick.IsChecked=data.DoubleClickHide;if(desktopEnabled!=null)desktopEnabled.IsChecked=controller.Running;if(autoUpdate!=null)autoUpdate.IsChecked=data.AutoRules;
        if(desktopDragCreate!=null)desktopDragCreate.IsChecked=data.DesktopDragCreate;
        if(controller.DragCreationActive)stats.Text+=" · 左键框选已启用";
        string workSignature=String.Join("|",data.WorkFolders)+"|"+Store.Json.Serialize(data.WorkSources)+"|"+data.IgnoreFiles.Count;if(CurrentSettingsPage=="AI 工作目录"&&workSignature!=lastWorkCards){lastWorkCards=workSignature;ShowSettingsPage("AI 工作目录");}
        string signature=Store.Json.Serialize(data)+"|"+controller.Search;if(signature==lastCards)return;lastCards=signature;cards.Children.Clear();
        foreach(var widget in data.Widgets.Where(w=>w.Page==data.Page))cards.Children.Add(WidgetCard(widget));
        foreach(var group in data.Groups.Where(g=>g.Page==data.Page))cards.Children.Add(Card(group));
        if(!data.Groups.Any(g=>g.Page==data.Page)&&!data.Widgets.Any(w=>w.Page==data.Page)){cards.Children.Add(Ui.Text("这一页还没有分组或组件。点击“新建分组”或“添加组件”开始。",16,Ui.Muted));}
    }
    Border WidgetCard(DesktopWidget widget){
        var plugin=WidgetPlugins.Find(controller.Store.Data,widget.PluginId);var panel=new StackPanel();var top=new DockPanel();
        var remove=Ui.Button("×",delegate{controller.Store.Checkpoint();controller.Store.Data.Widgets.Remove(widget);controller.SaveAndRefresh();});remove.ToolTip="删除组件";remove.Padding=new Thickness(7,1,7,1);DockPanel.SetDock(remove,Dock.Right);top.Children.Add(remove);
        var edit=Ui.Button("···",delegate{EditWidget(widget);});edit.Padding=new Thickness(7,1,7,1);DockPanel.SetDock(edit,Dock.Right);top.Children.Add(edit);
        var title=Ui.Text(widget.Name,17,Ui.Ink);title.FontWeight=FontWeights.SemiBold;top.Children.Add(title);panel.Children.Add(top);
        panel.Children.Add(Ui.Text("桌面组件 · "+(plugin==null?widget.PluginId:plugin.Name)+" · 第 "+(widget.Page+1)+" 页",12,Ui.Muted));
        var preview=new Border(){Height=92,Margin=new Thickness(0,14,0,12),CornerRadius=new CornerRadius(widget.CornerStyle=="方角"?0:12),Background=Appearance.Background(widget.Color,Math.Max(.25,widget.BackgroundOpacity),.35),BorderBrush=Ui.Brush(widget.Color),BorderThickness=new Thickness(1)};
        var label=Ui.Text(plugin==null?widget.Text:plugin.Kind=="clock"?DateTime.Now.ToString("HH:mm"):plugin.Kind=="calendar"?DateTime.Today.ToString("M 月 d 日"):plugin.Kind=="weather"?"☀  "+widget.Text:plugin.Kind=="calc"?"计算器":plugin.Kind=="system"?"系统状态":plugin.Kind=="photos"?"照片轮播":plugin.Kind=="habit"?(String.IsNullOrEmpty(widget.Text)?"习惯打卡":widget.Text):widget.Text,14,Ui.DesktopInk);label.TextTrimming=TextTrimming.CharacterEllipsis;label.VerticalAlignment=VerticalAlignment.Center;label.HorizontalAlignment=HorizontalAlignment.Center;preview.Child=label;panel.Children.Add(preview);
        var actions=new WrapPanel();actions.Children.Add(Ui.Button("设置组件",delegate{EditWidget(widget);}));panel.Children.Add(actions);
        return new Border(){Width=250,MinHeight=204,Padding=new Thickness(16),Margin=new Thickness(0,0,16,16),CornerRadius=new CornerRadius(14),Background=Ui.Surface,BorderBrush=Ui.Brush(widget.Color),BorderThickness=new Thickness(1),Child=panel,Effect=Ui.Shadow(5,16,.16)};
    }
    Border Card(Group g){
        var p=new StackPanel();var top=new Grid();top.ColumnDefinitions.Add(new ColumnDefinition());top.ColumnDefinitions.Add(new ColumnDefinition(){Width=GridLength.Auto});
        p.ContextMenu=new ContextMenu();
        p.ContextMenu.Items.Add(Ui.Menu("在桌面显示分区",delegate{controller.ShowDesktopGroup(g);}));
        p.ContextMenu.Items.Add(Ui.Menu(controller.SelectedGroups.Contains(g.Id)?"取消工作区选择":"选择为工作区",delegate{controller.ToggleGroupSelection(PartitionIgnores.Owner(controller.Store.Data,g));}));
        p.ContextMenu.Items.Add(Ui.Menu("规则与自动归类…",delegate{EditPartitionRules(g);}));
        var label=Ui.Text(g.Name,17,Ui.Ink);label.FontWeight=FontWeights.SemiBold;top.Children.Add(label);var actions=new StackPanel(){Orientation=Orientation.Horizontal};var edit=Ui.Button("···",delegate{EditGroup(g);});edit.Padding=new Thickness(7,1,7,1);actions.Children.Add(edit);var remove=Ui.Button("×",delegate{DeleteGroup(g);});remove.ToolTip="删除分区，项目恢复到桌面";remove.Padding=new Thickness(7,1,7,1);actions.Children.Add(remove);Grid.SetColumn(actions,1);top.Children.Add(actions);p.Children.Add(top);
        string[] items=controller.Store.Items(g,controller.Search).ToArray();p.Children.Add(Ui.Text((String.IsNullOrEmpty(g.Folder)?"桌面分组":"文件夹映射")+" · "+items.Length+" 个项目",12,Ui.Muted));
        var preview=new WrapPanel(){Margin=new Thickness(0,12,0,12)};foreach(string path in items.Take(6)){
            var item=new StackPanel(){Width=68,Margin=new Thickness(0,0,8,8)};var miniIcon=new Image(){Source=FileIconPresentation.Themed(path,g,controller.Store.Data,64)??Shell.Icon(path),Width=26,Height=26,Stretch=Stretch.Uniform};RenderOptions.SetBitmapScalingMode(miniIcon,BitmapScalingMode.HighQuality);item.Children.Add(new Border(){Width=36,Height=36,CornerRadius=new CornerRadius(10),Background=AppIconSkins.IsPack(g.IconSkin)?(Brush)Brushes.Transparent:new LinearGradientBrush(Color.FromRgb(43,63,87),Color.FromRgb(23,38,58),90),BorderBrush=Ui.Brush(g.Color),BorderThickness=new Thickness(AppIconSkins.IsPack(g.IconSkin)?0:1),Child=miniIcon,Margin=new Thickness(0,0,0,5)});item.Children.Add(new TextBlock(){Text=FilePresentation.DisplayName(path,controller.Store.Data.ShowFileExtensions),FontSize=10,Foreground=Ui.Muted,TextTrimming=TextTrimming.CharacterEllipsis,TextAlignment=TextAlignment.Center});preview.Children.Add(item);
        }p.Children.Add(preview);if(items.Length==0)p.Children.Add(Ui.Text("准备好接收你的项目",12,Ui.Muted));
        var border=new Border(){Width=250,MinHeight=204,Padding=new Thickness(16),Margin=new Thickness(0,0,16,16),CornerRadius=new CornerRadius(14),Background=Ui.Surface,BorderBrush=Ui.Brush(g.Color),BorderThickness=new Thickness(1),Child=p,Effect=Ui.Shadow(5,16,.16),AllowDrop=true};
        border.DragOver+=delegate(object s,DragEventArgs e){e.Effects=!String.IsNullOrEmpty(g.Folder)?DragTransfer.Effect(e,g.Folder):Functions.DragPaths(e.Data)!=null?DragDropEffects.Link:DragDropEffects.None;e.Handled=true;};border.Drop+=delegate(object s,DragEventArgs e){if(!String.IsNullOrEmpty(g.Folder)){DragTransfer.Drop(this,controller,e,g.Folder,Refresh);return;}var paths=Functions.DragPaths(e.Data);if(paths==null)return;controller.Store.Checkpoint();foreach(string path in paths)controller.Store.Assign(path,g);controller.SaveAndRefresh();};return border;
    }
    public void DeleteGroup(Group g){
        controller.Store.Checkpoint();var groups=controller.Store.Data.Groups;var removed=groups.Where(x=>x==g||x.HostGroupId==g.Id).ToArray();
        foreach(var item in removed)groups.Remove(item);
        foreach(var item in removed)controller.SelectedGroups.Remove(item.Id);
        controller.Store.Data.SelectedWorkspaceGroups.RemoveAll(id=>removed.Any(item=>item.Id==id));
        controller.Store.Data.Rules.RemoveAll(r=>removed.Any(x=>x.Id==r.GroupId));
        controller.SaveAndRefresh();SetStatus("分区已删除；其中的桌面项目已恢复到桌面。可撤销此次更改。");
    }
    void AddGroup(){string name=Ui.Ask("新建分组","分组名称","新分组",this);if(String.IsNullOrWhiteSpace(name))return;controller.Store.Checkpoint();controller.Store.Data.Groups.Add(new Group(){Name=name,Page=controller.Store.Data.Page,X=60,Y=80});controller.SaveAndRefresh();}
    void AddPortal(){using(var dialog=new System.Windows.Forms.FolderBrowserDialog()){dialog.Description="选择要在桌面显示的文件夹";if(dialog.ShowDialog()!=System.Windows.Forms.DialogResult.OK)return;controller.Store.Checkpoint();string path=dialog.SelectedPath;controller.Store.Data.Groups.Add(new Group(){Name=String.IsNullOrEmpty(Path.GetFileName(path))?path:Path.GetFileName(path),Kind="文件夹",Folder=path,Folders=new System.Collections.Generic.List<string>{path},Page=controller.Store.Data.Page,X=80,Y=80});controller.SaveAndRefresh();}}
    public void EditGroup(Group g){
        var w=Ui.Dialog("编辑分组",430,580,this);var p=new StackPanel(){Margin=new Thickness(22)};p.Children.Add(Ui.Text("名称",12,Ui.Muted));var name=Ui.Input(g.Name);p.Children.Add(name);
        p.Children.Add(Ui.Text("颜色（如 #77BFA3）",12,Ui.Muted));var color=Ui.Input(g.Color);p.Children.Add(color);
        p.Children.Add(Ui.Text("分区边角",12,Ui.Muted));var corners=new ComboBox(){ItemsSource=new[]{"圆角","方角"},SelectedItem=g.CornerStyle=="方角"?"方角":"圆角",Margin=new Thickness(0,0,0,12)};p.Children.Add(corners);
        p.Children.Add(Ui.Text("页面编号",12,Ui.Muted));var pageInput=Ui.Input((g.Page+1).ToString());p.Children.Add(pageInput);
        p.Children.Add(Ui.Text("排序方式",12,Ui.Muted));var sort=new ComboBox(){ItemsSource=new[]{"名称","类型","修改时间","大小"},SelectedItem=g.Sort,Margin=new Thickness(0,0,0,16)};p.Children.Add(sort);
        p.Children.Add(Ui.Text("背景不透明度（0 为透明，边框和文字保持清晰）",12,Ui.Muted));var opacity=new Slider(){Minimum=0,Maximum=1,Value=g.BackgroundOpacity,Margin=new Thickness(0,0,0,16)};p.Children.Add(opacity);
        p.Children.Add(Ui.Text("边框与右下角手柄不透明度",12,Ui.Muted));var borderOpacity=new Slider(){Minimum=0,Maximum=1,Value=g.BorderOpacity,Margin=new Thickness(0,0,0,16)};p.Children.Add(borderOpacity);
        var locked=new CheckBox(){Content="锁定位置和尺寸",IsChecked=g.Locked,Margin=new Thickness(0,0,0,12)};p.Children.Add(locked);var keep=new CheckBox(){Content="全局隐藏时保留此分组",IsChecked=g.ExcludeFromHide,Margin=new Thickness(0,0,0,16)};p.Children.Add(keep);
        p.Children.Add(Ui.Button("保存",delegate{int n;if(!Int32.TryParse(pageInput.Text,out n)||n<1||n>20){Ui.Info("页面编号应为 1 到 20。",w);return;}try{new BrushConverter().ConvertFromString(color.Text);}catch{Ui.Info("颜色格式不正确。",w);return;}controller.Store.Checkpoint();g.Name=name.Text.Trim();g.Color=color.Text.Trim();g.CornerStyle=Convert.ToString(corners.SelectedItem);g.Page=n-1;controller.Store.Data.Pages=Math.Max(n,controller.Store.Data.Pages);g.Sort=Convert.ToString(sort.SelectedItem);g.BackgroundOpacity=opacity.Value;g.BorderOpacity=borderOpacity.Value;g.Locked=locked.IsChecked==true;g.ExcludeFromHide=keep.IsChecked==true;controller.SaveAndRefresh();w.Close();}));
        p.Children.Add(Ui.Button("移除这个分组",delegate{DeleteGroup(g);w.Close();}));
        p.Children.Add(Ui.Button("规则与自动归类…",delegate{w.Close();EditPartitionRules(g);}));w.Content=new ScrollViewer(){Content=p,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};w.ShowDialog();
    }
    void Import(){try{string directory=Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","research","evidence"));if(!Directory.Exists(directory))directory=Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);var d=new OpenFileDialog(){Title="选择只读导出的 Fences 配置",Filter="Fences 导出 JSON|*.json",InitialDirectory=directory};string exported=Path.Combine(directory,"fences-config.json");if(File.Exists(exported))d.FileName=exported;if(d.ShowDialog(this)!=true)return;controller.ImportFences(d.FileName);SetStatus("已导入布局；可用撤销恢复之前的分组。导入后的自动规则默认关闭。");}catch(Exception ex){Ui.Info("导入失败："+ex.Message,this);}}
    void Snapshots(){var w=Ui.Dialog("布局快照",620,420,this);var p=new StackPanel(){Margin=new Thickness(22)};p.Children.Add(Ui.Text("保存并恢复分组、归属与窗口布局",17,Ui.Ink));var list=new ListBox(){Height=240,Background=Ui.Surface,Foreground=Ui.Ink,ItemsSource=Directory.GetFiles(controller.Store.SnapshotRoot,"*.json").OrderByDescending(x=>x).Select(Path.GetFileName)};p.Children.Add(list);var actions=new WrapPanel(){Margin=new Thickness(0,16,0,0)};actions.Children.Add(Ui.Button("保存当前布局",delegate{controller.Store.Snapshot();w.Close();SetStatus("布局快照已保存。");}));actions.Children.Add(Ui.Button("恢复所选布局",delegate{if(list.SelectedItem==null)return;try{controller.Store.Restore(Path.Combine(controller.Store.SnapshotRoot,list.SelectedItem.ToString()));controller.SaveAndRefresh();w.Close();}catch(Exception ex){Ui.Info(ex.Message,w);}}));p.Children.Add(actions);w.Content=p;w.ShowDialog();}
    void EditRules(){
        var w=Ui.Dialog("整理规则",680,480,this);var p=new StackPanel(){Margin=new Thickness(22)};p.Children.Add(Ui.Text("规则按顺序匹配，第一条命中决定分组",17,Ui.Ink));
        var list=new ListBox(){Height=200,Background=Ui.Surface,Foreground=Ui.Ink};Action refresh=delegate{list.ItemsSource=controller.Store.Data.Rules.Select(r=>(r.Enabled?"✓ ":"○ ")+r.Name+"  ·  "+r.Extensions+"  ·  "+r.Contains).ToArray();};refresh();p.Children.Add(list);
        var actions=new WrapPanel(){Margin=new Thickness(0,14,0,0)};actions.Children.Add(Ui.Button("添加规则",delegate{RuleEditor(w);refresh();}));actions.Children.Add(Ui.Button("启用 / 停用",delegate{int i=list.SelectedIndex;if(i<0)return;controller.Store.Checkpoint();controller.Store.Data.Rules[i].Enabled=!controller.Store.Data.Rules[i].Enabled;controller.SaveAndRefresh();refresh();}));actions.Children.Add(Ui.Button("上移",delegate{int i=list.SelectedIndex;if(i<1)return;controller.Store.Checkpoint();var r=controller.Store.Data.Rules[i];controller.Store.Data.Rules.RemoveAt(i);controller.Store.Data.Rules.Insert(i-1,r);controller.SaveAndRefresh();refresh();}));actions.Children.Add(Ui.Button("移除规则",delegate{int i=list.SelectedIndex;if(i<0)return;controller.Store.Checkpoint();controller.Store.Data.Rules.RemoveAt(i);controller.SaveAndRefresh();refresh();}));p.Children.Add(actions);
        var auto=new CheckBox(){Content="自动归类新出现的桌面文件",IsChecked=controller.Store.Data.AutoRules,Margin=new Thickness(0,10,0,14)};auto.Click+=delegate{controller.Store.Data.AutoRules=auto.IsChecked==true;controller.SaveAndRefresh(false);};p.Children.Add(auto);p.Children.Add(Ui.Button("立即应用全部规则",controller.ApplyRules));w.Content=p;w.ShowDialog();
    }
    void RuleEditor(Window owner){
        var groups=controller.Store.Data.Groups.Where(g=>String.IsNullOrEmpty(g.Folder)).ToArray();if(groups.Length==0){Ui.Info("先创建一个普通分组。",owner);return;}
        var w=Ui.Dialog("添加整理规则",500,650,owner);var p=new StackPanel(){Margin=new Thickness(22)};
        var name=Ui.Input("新规则");p.Children.Add(Ui.Text("规则名称",12,Ui.Muted));p.Children.Add(name);
        var kind=Choice(p,"匹配对象",new[]{"全部","文件","文件夹"},0);
        var ext=Ui.Input("");p.Children.Add(Ui.Text("扩展名，以逗号分隔；留空匹配全部类型",12,Ui.Muted));p.Children.Add(ext);
        var mode=Choice(p,"文件名条件",new[]{"包含","开头","结尾"},0);
        var text=Ui.Input("");p.Children.Add(Ui.Text("匹配文字（留空匹配全部名称）",12,Ui.Muted));p.Children.Add(text);
        var target=Choice(p,"目标分组",groups.Select(g=>g.Name),0);
        p.Children.Add(Ui.Button("保存规则",delegate{controller.Store.Checkpoint();controller.Store.Data.Rules.Add(new Rule(){Name=name.Text.Trim(),Extensions=ext.Text.Trim(),Contains=text.Text,NameMode=Convert.ToString(mode.SelectedItem),Kind=Convert.ToString(kind.SelectedItem),GroupId=groups[target.SelectedIndex].Id});controller.SaveAndRefresh();w.Close();}));
        w.Content=new ScrollViewer(){Content=p,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};w.ShowDialog();
    }
    void AiSettings(){var w=Ui.Dialog("模型设置",570,330,this);var p=new StackPanel(){Margin=new Thickness(22)};p.Children.Add(Ui.Text("支持本地 Ollama 和兼容 Chat Completions 的服务",14,Ui.Ink));TextBox endpoint=Ui.Input(controller.Store.Data.AiEndpoint),model=Ui.Input(controller.Store.Data.AiModel);p.Children.Add(Ui.Text("服务地址：Ollama 根地址，或兼容服务的 /v1 地址",12,Ui.Muted));p.Children.Add(endpoint);p.Children.Add(Ui.Text("模型名称",12,Ui.Muted));p.Children.Add(model);p.Children.Add(Ui.Text("兼容服务密钥从 QIDESK_AI_KEY 环境变量读取。仅发送文件名、扩展名和分组名。",11,Ui.Muted));p.Children.Add(Ui.Button("保存",delegate{controller.Store.Data.AiEndpoint=endpoint.Text.Trim();controller.Store.Data.AiModel=model.Text.Trim();controller.SaveAndRefresh(false);w.Close();}));w.Content=p;w.ShowDialog();}
    async void GetAi(){
        SetStatus("正在请求模型建议…");
        try{
            var progress=new Action<string>(delegate(string text){Dispatcher.BeginInvoke(new Action(delegate{SetStatus(text);}));});
            var list=await Task.Run(()=>AiClient.Recommend(controller.Store,progress));
            ShowSuggestions(list,"AI 分类建议");
            SetStatus("AI 建议已返回；应用前可逐项选择。");
        }catch(Exception ex){SetStatus("模型请求失败："+ex.Message);}
    }
    void ShowSuggestions(List<Suggestion> suggestions,string title){var w=Ui.Dialog(title,760,520,this);var p=new DockPanel(){Margin=new Thickness(22)};var bottom=new StackPanel();DockPanel.SetDock(bottom,Dock.Bottom);bottom.Children.Add(Ui.Text(suggestions.Count+" 条建议 · 选择后应用，可撤销",12,Ui.Muted));var selected=new List<Tuple<CheckBox,Suggestion>>();bottom.Children.Add(Ui.Button("应用选中的建议",delegate{controller.ApplySuggestions(selected.Where(x=>x.Item1.IsChecked==true).Select(x=>x.Item2));w.Close();}));p.Children.Add(bottom);var rows=new StackPanel();foreach(var s in suggestions){var check=new CheckBox(){Content=s.ToString(),IsChecked=true,Margin=new Thickness(0,0,0,14)};selected.Add(Tuple.Create(check,s));rows.Children.Add(check);}if(suggestions.Count==0)rows.Children.Add(Ui.Text("当前没有需要调整的项目。",16,Ui.Ink));p.Children.Add(new ScrollViewer(){Content=rows});w.Content=p;w.ShowDialog();}
}
}
