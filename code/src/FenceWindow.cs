using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;

namespace QiDesk {
public partial class FenceWindow : Window {
    public readonly Group Group;
    readonly Controller controller;
    WrapPanel tiles=new WrapPanel();
    ScrollViewer scroll;
    TextBlock heading;
    Border frame;
    Border titlePlate;
    Thumb resize;
    bool updating;
    Point dragStart;
    double startX,startY;
    bool moving;
    bool groupMoving;
    Dictionary<string,Point> groupMoveOrigins;
    bool transferPageArmed;
    bool hoverExpanded;
    PortalSession portal;
    bool transientPortal;
    string previewPath;
    List<PortalSession> sessions=new List<PortalSession>();
    StackPanel sectionTabs;
    ScrollViewer tabsScroll;
    string lastTabsSignature=null;
    TextBlock backIcon;
    Border ignoreTarget;
    Border selectTarget,unpinTarget,deleteTarget;
    TextBlock selectGlyph;
    bool editingTab;
    StackPanel headerItems;
    int HeaderFont {get{return Math.Max(17,(int)Math.Round(Group.LabelSize));}}
    Group ViewGroup {get{var selected=controller.Store.Data.Groups.FirstOrDefault(g=>g.Id==Group.ActiveGroupId&&g.HostGroupId==Group.Id);return selected??Group;}}
    Group FolderGroup {get{return PartitionTabs.FolderOwner(controller.Store.Data,Group);}}
    bool IsFolderPanel {get{return FolderGroup.Kind=="文件夹"||FolderGroup.Folders.Count>0;}}
    public bool Docked;
    public FenceWindow(Controller owner,Group group) {
        controller=owner;Group=group;
        Title=group.Name;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;
        AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;ShowActivated=false;
        FontFamily=new FontFamily("Microsoft YaHei UI");Foreground=Ui.DesktopInk;MinWidth=220;MinHeight=48;
        frame=new Border(){CornerRadius=new CornerRadius(16),BorderThickness=new Thickness(1.5),BorderBrush=Ui.Brush(group.Color),Background=Brushes.Transparent,Effect=Ui.Shadow(3,12,.30)};
        var grid=new Grid();grid.RowDefinitions.Add(new RowDefinition(){Height=new GridLength(GroupArrangement.HeaderHeight)});grid.RowDefinitions.Add(new RowDefinition(){Height=GridLength.Auto});grid.RowDefinitions.Add(new RowDefinition(){Height=new GridLength(1,GridUnitType.Star)});
        var header=new Grid(){Background=Brushes.Transparent,Margin=new Thickness(12,6,8,4)};
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition(){Width=GridLength.Auto});
        header.ColumnDefinitions.Add(new ColumnDefinition(){Width=GridLength.Auto});
        header.ColumnDefinitions.Add(new ColumnDefinition(){Width=GridLength.Auto});
        header.ColumnDefinitions.Add(new ColumnDefinition(){Width=GridLength.Auto});
        heading=Ui.Text(group.Name,15,Ui.DesktopInk);heading.FontWeight=FontWeights.SemiBold;heading.VerticalAlignment=VerticalAlignment.Center;heading.Margin=new Thickness(0,5,0,0);
        headerItems=new StackPanel(){Orientation=Orientation.Horizontal,ClipToBounds=true};
        backIcon=Ui.Text("←",23,Ui.DesktopInk);backIcon.Cursor=Cursors.Hand;backIcon.Margin=new Thickness(0,0,10,0);backIcon.Visibility=Visibility.Collapsed;backIcon.ToolTip="返回上一层";
        backIcon.MouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e){if(previewPath!=null)ClosePreview();else NavigateBack();e.Handled=true;};
        headerItems.Children.Add(backIcon);headerItems.Children.Add(heading);
        // 一条标签栏容纳自身、文件夹映射和合并分区；三者只有内容来源不同。
        sectionTabs=new StackPanel(){Orientation=Orientation.Horizontal};
        tabsScroll=new ScrollViewer(){Content=sectionTabs,HorizontalScrollBarVisibility=ScrollBarVisibility.Hidden,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,Margin=new Thickness(6,0,2,0),MaxWidth=420};
        tabsScroll.PreviewMouseWheel+=delegate(object s,MouseWheelEventArgs e){tabsScroll.ScrollToHorizontalOffset(tabsScroll.HorizontalOffset-Math.Sign(e.Delta)*90);e.Handled=true;};
        tabsScroll.AllowDrop=true;
        tabsScroll.DragOver+=delegate(object s,DragEventArgs e){e.Effects=Functions.DragPaths(e.Data)!=null?DragDropEffects.Link:DragDropEffects.None;e.Handled=true;};
        tabsScroll.Drop+=delegate(object s,DragEventArgs e){e.Handled=true;AddFolders(Functions.DragPaths(e.Data));};
        headerItems.Children.Add(tabsScroll);header.Children.Add(headerItems);
        var ignoreGlyph=Ui.Text("⊘",17,Ui.DesktopMuted);ignoreGlyph.Margin=new Thickness(0);ignoreGlyph.VerticalAlignment=VerticalAlignment.Center;ignoreGlyph.HorizontalAlignment=HorizontalAlignment.Center;
        ignoreTarget=new Border(){Child=ignoreGlyph,Width=28,Height=28,Background=Brushes.Transparent,BorderBrush=Ui.Brush(group.Color),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(7),AllowDrop=true,Cursor=Cursors.Hand,ToolTip="拖入项目，忽略当前分区或文件夹标签",Opacity=0,IsHitTestVisible=false};
        selectGlyph=Ui.Text("◯",22,Ui.DesktopInk);selectGlyph.Margin=new Thickness(0);selectGlyph.HorizontalAlignment=HorizontalAlignment.Center;selectGlyph.VerticalAlignment=VerticalAlignment.Center;
        selectTarget=new Border(){Child=selectGlyph,Width=28,Height=28,Background=Brushes.Transparent,CornerRadius=new CornerRadius(7),Cursor=Cursors.Hand,ToolTip="选择此工作区；Ctrl＋左键可多选",Opacity=0,IsHitTestVisible=false};
        selectTarget.MouseLeftButtonDown+=delegate(object s,MouseButtonEventArgs e){e.Handled=true;controller.ToggleGroupSelection(Group);};
        Grid.SetColumn(selectTarget,1);header.Children.Add(selectTarget);
        var unpinGlyph=Ui.Text("⌖",17,Ui.DesktopMuted);unpinGlyph.HorizontalAlignment=HorizontalAlignment.Center;unpinGlyph.VerticalAlignment=VerticalAlignment.Center;
        unpinTarget=new Border(){Child=unpinGlyph,Width=28,Height=28,Background=Brushes.Transparent,CornerRadius=new CornerRadius(7),Cursor=Cursors.Hand,ToolTip="从工作区选择中移除；桌面分区保留",Opacity=0,IsHitTestVisible=false};
        unpinTarget.MouseLeftButtonDown+=delegate(object s,MouseButtonEventArgs e){e.Handled=true;if(Selected)controller.ToggleGroupSelection(Group);};
        Grid.SetColumn(unpinTarget,2);header.Children.Add(unpinTarget);
        Grid.SetColumn(ignoreTarget,3);header.Children.Add(ignoreTarget);
        var deleteGlyph=Ui.Text("×",24,Ui.DesktopInk);deleteGlyph.Margin=new Thickness(0);deleteGlyph.HorizontalAlignment=HorizontalAlignment.Center;deleteGlyph.VerticalAlignment=VerticalAlignment.Center;
        deleteTarget=new Border(){Child=deleteGlyph,Width=28,Height=28,Background=Brushes.Transparent,CornerRadius=new CornerRadius(7),Cursor=Cursors.Hand,ToolTip="删除此分区；可在管理面板撤销",Opacity=0,IsHitTestVisible=false};
        deleteTarget.ToolTip="收起此分区；Alt＋Space 可唤起已选分区";
        deleteTarget.MouseLeftButtonDown+=delegate(object s,MouseButtonEventArgs e){e.Handled=true;controller.HideWorkspaceGroup(Group);};
        Grid.SetColumn(deleteTarget,4);header.Children.Add(deleteTarget);
        unpinTarget.Visibility=Visibility.Collapsed;
        foreach(var target in new[]{selectTarget,ignoreTarget,deleteTarget}){target.Width=32;target.Height=32;target.Margin=new Thickness(2,0,0,0);((TextBlock)target.Child).Foreground=Ui.DesktopInk;}
        Action<bool> showHeaderActions=delegate(bool visible){foreach(var target in new[]{selectTarget,ignoreTarget,deleteTarget}){target.Opacity=visible?1:0;target.IsHitTestVisible=visible;}};
        header.MouseEnter+=delegate{showHeaderActions(true);};
        header.MouseLeave+=delegate{showHeaderActions(false);};
        header.DragEnter+=delegate{showHeaderActions(true);};
        header.DragLeave+=delegate{if(!header.IsMouseOver)showHeaderActions(false);};
        ignoreTarget.MouseLeftButtonDown+=delegate(object s,MouseButtonEventArgs e){e.Handled=true;ignoreTarget.ContextMenu=BuildIgnoreMenu();ignoreTarget.ContextMenu.IsOpen=true;};
        ignoreTarget.ContextMenu=new ContextMenu();ignoreTarget.ContextMenuOpening+=delegate{ignoreTarget.ContextMenu=BuildIgnoreMenu();};
        ignoreTarget.DragOver+=delegate(object s,DragEventArgs e){e.Effects=Functions.DragPaths(e.Data)!=null?DragDropEffects.Link:DragDropEffects.None;e.Handled=true;};
        ignoreTarget.Drop+=delegate(object s,DragEventArgs e){e.Handled=true;IgnoreDropped(Functions.DragPaths(e.Data));};
        header.MouseLeftButtonDown+=delegate(object s,MouseButtonEventArgs e){
            DependencyObject part=e.OriginalSource as DependencyObject;while(part!=null&&part!=header){if(part is ButtonBase||part is TextBox)return;part=part is Visual?VisualTreeHelper.GetParent(part):LogicalTreeHelper.GetParent(part);}
            if((Keyboard.Modifiers&ModifierKeys.Control)!=0){controller.ToggleGroupSelection(Group);e.Handled=true;return;}
            if(e.ClickCount==2){if(e.OriginalSource==heading)RenameGroup(Group);else ToggleRoll();e.Handled=true;return;}
            if(Group.Locked)return;
            StopHoverExpansion();
            if(Group.Rolled&&hoverExpanded){hoverExpanded=false;ApplyBounds();}
            controller.Store.Checkpoint();
            var targets=GroupMoveTargets();
            groupMoving=Selected&&targets.Length>1;
            groupMoveOrigins=null;
            if(groupMoving){
                groupMoveOrigins=new Dictionary<string,Point>();
                foreach(var window in targets)groupMoveOrigins[window.Group.Id]=new Point(window.Group.X,window.Group.Y);
                if(!groupMoveOrigins.ContainsKey(Group.Id))groupMoveOrigins[Group.Id]=new Point(Group.X,Group.Y);
            }
            moving=true;transferPageArmed=false;dragStart=PointToScreen(e.GetPosition(this));startX=Left;startY=Top;header.CaptureMouse();e.Handled=true;
        };
        header.MouseMove+=delegate(object s,MouseEventArgs e){
            if(!moving)return;
            if(e.LeftButton!=MouseButtonState.Pressed){moving=false;header.ReleaseMouseCapture();ApplyBounds();return;}
            Point point=PointToScreen(e.GetPosition(this));
            double scale=Shell.WindowScale(this);
            double dx=(point.X-dragStart.X)/scale,dy=(point.Y-dragStart.Y)/scale;
            if(Math.Abs(dx)<SystemParameters.MinimumHorizontalDragDistance&&Math.Abs(dy)<SystemParameters.MinimumVerticalDragDistance)return;
            if(groupMoving){
                foreach(var window in controller.Windows){
                    Point origin;
                    if(!groupMoveOrigins.TryGetValue(window.Group.Id,out origin))continue;
                    window.Group.X=origin.X+dx;window.Group.Y=origin.Y+dy;window.Group.Edge="";
                    window.Left=window.Group.X;window.Top=window.Group.Y;
                }
                heading.Text=Group.Name+"  ·  "+groupMoveOrigins.Count+" 个分区";
                return;
            }
            Left=startX+dx;Top=startY+dy;
            if(e.RightButton==MouseButtonState.Pressed)transferPageArmed=true;
            int destination=GroupPages.Destination(Group.Page,controller.Store.Data.Pages,Left-startX,transferPageArmed);
            if(destination!=Group.Page)heading.Text=Group.Name+"  →  第 "+(destination+1)+" 页";
        };
        header.LostMouseCapture+=delegate{if(!moving)return;moving=false;if(groupMoving){RestoreGroupMove();groupMoving=false;groupMoveOrigins=null;}ApplyBounds();};
        header.MouseRightButtonDown+=delegate(object s,MouseButtonEventArgs e){if(moving){transferPageArmed=true;e.Handled=true;}};
        // 中键：隐藏 / 收起这个分区；再按 Alt＋Space 或中键点标题可以把它收回最前面。
        header.MouseDown+=MiddleClickHandler;
        header.MouseLeftButtonUp+=delegate(object s,MouseButtonEventArgs e){
            if(!moving)return;
            Point drop=PointToScreen(e.GetPosition(this));moving=false;header.ReleaseMouseCapture();
            bool moved=Math.Abs(Left-startX)+Math.Abs(Top-startY)>=6;
            if(groupMoving){
                if(moved)FinishGroupMove();
                groupMoving=false;groupMoveOrigins=null;heading.Text=Group.Name;
                e.Handled=true;return;
            }
            if(!moved){if(e.OriginalSource==heading&&Group.ActiveGroupId!=Group.Id)SelectGroup(Group);return;}
            if(!Selected&&controller.TryMergeGroup(Group,drop,this))return;
            if(!controller.TransferGroupPage(Group,Left-startX,transferPageArmed,new Point(Left,Top),Shell.WorkingAreas(this)))SaveBounds();
        };
        scroll=new ScrollViewer(){Content=tiles,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Margin=new Thickness(6,0,6,12)};
        scroll.Loaded+=delegate{Ui.SetVerticalScrollBarOpacity(scroll,scroll.IsMouseOver?1:0);};
        scroll.MouseEnter+=delegate{Ui.SetVerticalScrollBarOpacity(scroll,1);};
        scroll.MouseLeave+=delegate{Ui.SetVerticalScrollBarOpacity(scroll,0);};
        titlePlate=new Border(){Child=header,CornerRadius=new CornerRadius(14,14,6,6),Margin=new Thickness(3),Background=new SolidColorBrush(Color.FromArgb(100,18,27,42))};
        // 中键点标题栏（不是文件、也不是标签）同样收起 / 收回这个分区。
        titlePlate.MouseDown+=MiddleClickHandler;
        Grid.SetRow(scroll,2);grid.Children.Add(titlePlate);grid.Children.Add(scroll);
        EnsureSessions();
        header.AllowDrop=true;
        header.DragOver+=delegate(object s,DragEventArgs e){var paths=Functions.DragPaths(e.Data);e.Effects=paths!=null&&paths.Any(Directory.Exists)?DragDropEffects.Link:DragDropEffects.None;e.Handled=true;};
        header.Drop+=delegate(object s,DragEventArgs e){e.Handled=true;AddFolders(Functions.DragPaths(e.Data));};
        resize=AddResizeHandle(grid,HorizontalAlignment.Right,16,Cursors.SizeNWSE,1,group.Color);
        AddResizeHandle(grid,HorizontalAlignment.Left,16,Cursors.SizeNESW,-1,group.Color);
        AddResizeHandle(grid,HorizontalAlignment.Center,64,Cursors.SizeNS,0,group.Color);
        var grip=new TextBlock(){Text="⠿",FontSize=20,Foreground=Ui.DesktopInk,Width=24,VerticalAlignment=VerticalAlignment.Center,TextAlignment=TextAlignment.Center,Cursor=Cursors.SizeAll,ToolTip="拖动分区（折叠时也可拖动）"};
        headerItems.Children.Add(grip);
        frame.Child=grid;Content=frame;AllowDrop=true;
        InitializeFileRenaming();InitializeSearch(grid);
        // 中键在标题栏、标题上层、分区空白处和文件区都生效；落在文件图标上时交给文件自己的行为。
        MouseDown+=MiddleClickHandler;
        scroll.MouseDown+=MiddleClickHandler;
        PreviewMouseWheel+=delegate(object sender,MouseWheelEventArgs e){if((Keyboard.Modifiers&ModifierKeys.Control)==0)return;e.Handled=true;string before=ViewGroup.ViewMode+ViewGroup.IconSize;controller.Store.Checkpoint();int steps=Math.Max(1,Math.Abs(e.Delta)/120);for(int i=0;i<Math.Min(steps,10);i++)FilePresentation.Zoom(ViewGroup,Math.Sign(e.Delta));if(before!=ViewGroup.ViewMode+ViewGroup.IconSize){lastSignature=null;controller.SaveAndRefresh(false);}};
        PreviewKeyDown+=delegate(object sender,KeyEventArgs e){if(e.Handled||fileNameEditor!=null)return;Key key=e.Key==Key.System?e.SystemKey:e.Key;if(Selected&&!(SearchVisible&&key==Key.Escape)){if(key==Key.Enter){controller.ToggleSelectedWorkspace();e.Handled=true;return;}if(key==Key.Escape){controller.ClearGroupSelection();e.Handled=true;return;}}if(portal!=null&&(Keyboard.Modifiers&ModifierKeys.Alt)!=0&&(key==Key.Left||key==Key.Up)){if(key==Key.Left){portal.Back();lastSignature=null;Refresh();}else NavigateBack();e.Handled=true;return;}if(e.Key==Key.F5){lastSignature=null;Refresh();e.Handled=true;}};
        DragOver+=delegate(object s,DragEventArgs e){e.Effects=portal!=null?DragTransfer.Effect(e,portal.Current):Functions.DragPaths(e.Data)!=null&&(e.AllowedEffects&DragDropEffects.Link)!=0?DragDropEffects.Link:DragDropEffects.None;e.Handled=true;};
        Drop+=delegate(object s,DragEventArgs e){if(portal!=null){DragTransfer.Drop(this,controller,e,portal.Current,delegate{lastSignature=null;Refresh();});return;}var paths=Functions.DragPaths(e.Data);if(paths==null)return;controller.Store.Checkpoint();foreach(string path in paths)controller.Store.Assign(path,ViewGroup);controller.SaveAndRefresh();e.Handled=true;};
        ContextMenu=new ContextMenu();
        ContextMenuOpening+=delegate(object sender,ContextMenuEventArgs e){e.Handled=true;if(moving||controller.Manager==null)return;Point point=e.CursorLeft<0?PointToScreen(new Point(24,24)):PointToScreen(Mouse.GetPosition(this));controller.Manager.ShowFileMenu(this,FilePresentation.OpenFolder(ViewGroup,portal==null?ViewGroup.Folder:portal.Current),ViewGroup,point,true,delegate{lastSignature=null;Refresh();});};
        bool scrollPending=false;scroll.ScrollChanged+=delegate{if(scrollPending)return;scrollPending=true;Dispatcher.BeginInvoke(new Action(delegate{scrollPending=false;Thumbnails.RequestVisible(scroll);}),System.Windows.Threading.DispatcherPriority.Background);};

        MouseEnter+=delegate{ScheduleHoverExpansion();UpdateTitle();};
        MouseLeave+=delegate{StopHoverExpansion();if(hoverExpanded&&!moving){hoverExpanded=false;ApplyBounds();}UpdateTitle();};
        // Persist only completed user drags, never native ownership / DPI / activation events.
        ApplyBounds();Refresh();
    }
    Thumb AddResizeHandle(Grid grid,HorizontalAlignment alignment,double width,Cursor cursor,int side,string color){
        var handle=new Thumb(){Width=width,Height=side==0?10:16,HorizontalAlignment=alignment,VerticalAlignment=VerticalAlignment.Bottom,Cursor=cursor,Opacity=.08,Background=Ui.Brush(color)};
        Grid.SetRow(handle,2);grid.Children.Add(handle);
        handle.DragStarted+=delegate{if(!Group.Locked&&!Group.Rolled)controller.Store.Checkpoint();};
        handle.DragDelta+=delegate(object s,DragDeltaEventArgs e){if(Group.Locked||Group.Rolled)return;
            if(side<0){double change=Math.Min(e.HorizontalChange,Width-220);Left+=change;Width-=change;}
            else if(side>0)Width=Math.Max(220,Width+e.HorizontalChange);
            Height=Math.Max(100,Height+e.VerticalChange);
        };
        handle.MouseEnter+=delegate{handle.Opacity=.5;};handle.MouseLeave+=delegate{handle.Opacity=.08;};handle.Opacity=.08;
        handle.DragCompleted+=delegate{SaveBounds();};return handle;
    }
    // 命中的是不是文件/文件夹图标本身；是的话中键交给原来的行为（打开、拖动、拖放），不收起分区。
    static bool OnItem(object source){
        var node=source as DependencyObject;
        while(node!=null){
            var border=node as Border;
            if(border!=null&&String.Equals(Convert.ToString(border.Tag),"qidesk-file",StringComparison.Ordinal))return true;
            node=node is Visual?VisualTreeHelper.GetParent(node):null;
        }
        return false;
    }
    // 分区上的中键：没收起就收起，已收起就收回最前。Alt＋Space 对整组选中分区走的是同一套控制器逻辑。
    void MiddleClickHandler(object sender,MouseButtonEventArgs e){
        if(e.ChangedButton!=MouseButton.Middle)return;
        if(OnItem(e.OriginalSource))return;
        e.Handled=true;
        controller.ToggleGroupVisibility(Group);
    }
    // 中键手势与 Alt＋Space 对整组选中分区走同一个控制器入口；文件图标上的中键由 OnItem 放行。
    public void MiddleClick(){controller.ToggleGroupVisibility(Group);}
    List<IgnoreRule> CurrentIgnoreRules(){return Group.IgnoreRules;}
    ContextMenu BuildIgnoreMenu(){
        var menu=new ContextMenu();var rules=CurrentIgnoreRules();var global=controller.Store.Data.IgnoreRules;
        menu.Items.Add(new MenuItem(){Header="全局规则 · "+global.Count+" 条",IsEnabled=false});
        foreach(var rule in global.Take(12).ToArray()){var current=rule;bool removed=Group.DisabledGlobalIgnoreIds.Contains(current.Id);menu.Items.Add(Ui.Menu((removed?"恢复继承 · ":"仅此分区取消 · ")+current.Pattern,delegate{controller.Store.Checkpoint();if(removed)Group.DisabledGlobalIgnoreIds.Remove(current.Id);else Group.DisabledGlobalIgnoreIds.Add(current.Id);controller.Store.Save();lastSignature=null;Refresh();}));}
        menu.Items.Add(new Separator());
        menu.Items.Add(new MenuItem(){Header="当前分区 · "+(portal==null?ViewGroup.Name:FolderLabel(ViewGroup.Folders[ViewGroup.FolderIndex]))+" · "+rules.Count+" 条",IsEnabled=false});
        foreach(var rule in rules.Take(12).ToArray()){var current=rule;menu.Items.Add(Ui.Menu("取消当前忽略 · "+current.Pattern,delegate{rules.Remove(current);controller.Store.Save();lastSignature=null;Refresh();}));}
        menu.Items.Add(new Separator());
        menu.Items.Add(Ui.Menu("编辑分区与全局忽略规则",delegate{if(controller.Manager!=null)controller.Manager.ShowIgnoreRules(Group);}));
        menu.Items.Add(Ui.Menu("搜索此分区 · Ctrl＋F",ToggleSearchBar));
        return menu;
    }
    void IgnoreDropped(string[] paths){
        if(paths==null)return;
        var rules=CurrentIgnoreRules();var pending=new List<IgnoreRule>();
        foreach(string path in paths){
            if(!Store.Exists(path)||PanelFiles.HasExactIgnore(rules.Concat(pending),path))continue;
            pending.Add(PanelFiles.MakeIgnoreRule(path));
        }
        if(pending.Count==0)return;
        try{IgnoreRule.Validate(rules.Concat(pending).ToList());}
        catch(Exception ex){if(controller.Manager!=null)controller.Manager.SetStatus("忽略规则未添加："+ex.Message);return;}
        controller.Store.Checkpoint();rules.AddRange(pending);controller.Store.Save();lastSignature=null;Refresh();
        if(controller.Manager!=null)controller.Manager.SetStatus("已在此分区忽略 "+pending.Count+" 项；点击右上角忽略图标可撤销。");
    }
    void RenameGroup(Group target){
        if(target==null)return;var label=target==Group?(TextBlock)heading:null;
        if(label==null)return;
        var editor=new TextBox(){Text=target.Name,MinWidth=80,MaxWidth=180,FontSize=14,Margin=new Thickness(0,4,0,0)};int index=headerItems.Children.IndexOf(label);headerItems.Children.RemoveAt(index);headerItems.Children.Insert(index,editor);
        bool done=false;Action<bool> finish=delegate(bool save){if(done)return;done=true;if(save&&!String.IsNullOrWhiteSpace(editor.Text)&&editor.Text.Trim()!=target.Name){controller.Store.Checkpoint();target.Name=editor.Text.Trim();controller.Store.Save();}headerItems.Children.Remove(editor);headerItems.Children.Insert(index,label);Refresh();};
        editor.KeyDown+=delegate(object s,KeyEventArgs e){if(e.Key==Key.Enter){finish(true);e.Handled=true;}else if(e.Key==Key.Escape){finish(false);e.Handled=true;}};editor.LostKeyboardFocus+=delegate{finish(true);};editor.Focus();editor.SelectAll();
    }
    void ToggleRoll(){if(!controller.Store.Data.RollupEnabled)return;controller.Store.Checkpoint();Group.Rolled=!Group.Rolled;hoverExpanded=false;ApplyBounds();controller.Store.Save();}
    FenceWindow[] GroupMoveTargets(){
        return controller.Windows.Where(w=>w.IsVisible&&w.Group.Page==Group.Page&&controller.SelectedGroups.Contains(w.Group.Id)&&String.IsNullOrEmpty(w.Group.HostGroupId)&&!w.Group.Locked).ToArray();
    }
    // 多选移动：所有选中窗口跟手位移；松手时只对当前分区做一次网格吸附，把同样的偏移应用到整组。
    void FinishGroupMove(){
        if(groupMoveOrigins==null||groupMoveOrigins.Count==0)return;
        double beforeX=Group.X,beforeY=Group.Y,beforeWidth=Group.Width,beforeHeight=Group.Height;
        if(controller.Store.Data.SnapEnabled&&(Keyboard.Modifiers&ModifierKeys.Shift)==0){
            var others=controller.Store.Data.Groups.Where(g=>g.Page==Group.Page&&!groupMoveOrigins.ContainsKey(g.Id));
            Layout.Snap(Group,others,controller.Store.Data.SnapGrid,controller.Store.Data.SnapGap);
        }
        double snapDx=Group.X-beforeX,snapDy=Group.Y-beforeY;
        Group.Width=beforeWidth;Group.Height=beforeHeight;
        foreach(var window in controller.Windows){
            if(!groupMoveOrigins.ContainsKey(window.Group.Id))continue;
            if(!Object.ReferenceEquals(window.Group,Group)){window.Group.X+=snapDx;window.Group.Y+=snapDy;}
            window.Group.Edge="";
            window.Left=window.Group.X;window.Top=window.Group.Y;
        }
        controller.SaveAndRefresh(false);
    }
    void RestoreGroupMove(){
        if(groupMoveOrigins==null)return;
        foreach(var window in controller.Windows){
            Point origin;
            if(!groupMoveOrigins.TryGetValue(window.Group.Id,out origin))continue;
            window.Group.X=origin.X;window.Group.Y=origin.Y;window.Group.Edge="";
            window.Left=origin.X;window.Top=origin.Y;
        }
        heading.Text=Group.Name;
    }
    void SaveBounds(){if(updating||Group.Locked)return;Group.X=Left;Group.Y=Top;Group.Edge="";Group.Width=Width;if(!Group.Rolled)Group.Height=Height;
        if(controller.Store.Data.SnapEnabled&&(Keyboard.Modifiers&ModifierKeys.Shift)==0)Layout.Snap(Group,controller.Store.Data.Groups.Where(g=>g.Id!=Group.Id&&g.Page==Group.Page),controller.Store.Data.SnapGrid,controller.Store.Data.SnapGap);
        if(controller.Store.Data.RollupEnabled&&controller.Store.Data.EdgeRollup)foreach(var area in Shell.WorkingAreas(this)){if(area.Contains(new Point(Group.X+40,Group.Y+20))){if(Math.Abs(Group.Y-area.Top)<14){Group.Edge="top";Group.Y=area.Top;Group.Rolled=true;}else if(Math.Abs(Group.Y+Height-area.Bottom)<14){Group.Edge="bottom";Group.Y=area.Bottom-GroupArrangement.HeaderHeight;Group.Rolled=true;}break;}}
        ApplyBounds();controller.SaveAndRefresh(false);
    }
    void ApplyBounds(){
        updating=true;bool collapsed=Group.Rolled&&controller.Store.Data.RollupEnabled&&!hoverExpanded;
        Left=Group.X;Width=Group.Width;Height=collapsed?GroupArrangement.HeaderHeight:Group.Height;Top=Group.Edge=="bottom"&&!collapsed?Group.Y+GroupArrangement.HeaderHeight-Height:Group.Y;Opacity=1;
        double tabSpace=Math.Max(45,Group.Width-156);
        if(tabsScroll!=null){tabsScroll.MaxWidth=tabSpace;tabsScroll.Visibility=collapsed?Visibility.Collapsed:Visibility.Visible;}
        scroll.Visibility=collapsed?Visibility.Collapsed:Visibility.Visible;
        foreach(var handle in ((Grid)frame.Child).Children.OfType<Thumb>()){
            handle.Visibility=collapsed?Visibility.Collapsed:Visibility.Visible;
            handle.IsHitTestVisible=!Group.Rolled&&!Group.Locked;
        }
        updating=false;UpdateTitle();
    }
    public void RestoreSavedBounds(){ApplyBounds();}
    void UpdateTitle(){
        bool collapsed=Group.Rolled&&controller.Store.Data.RollupEnabled&&!hoverExpanded;
        heading.MaxWidth=Math.Max(40,Width-156);
        if(collapsed){heading.Text=ActiveTabTitle();heading.Visibility=Visibility.Visible;return;}
        // 顶栏标签已经承担标题；只有折叠成一条时才退回单行标题。
        heading.Visibility=Visibility.Collapsed;
    }
    string ActiveTabTitle(){
        foreach(var tab in PartitionTabs.Of(controller.Store.Data,Group))if(tab.Active)return tab.Title;
        return portal==null?ViewGroup.Name:FilePresentation.FolderName(portal.Current);
    }
    public void Refresh(){
        if(fileNameEditor!=null&&fileNameEditor.Editing)return;
        heading.Text=Group.Name+(String.IsNullOrEmpty(Group.Folder)?"":"  ↗");Title=Group.Name;
        var edgeColor=((SolidColorBrush)Ui.Brush(Group.Color)).Color;frame.BorderBrush=new SolidColorBrush(Color.FromArgb((byte)Math.Round(255*Group.BorderOpacity),edgeColor.R,edgeColor.G,edgeColor.B));Opacity=1;ApplySelectionVisual();
        bool square=Group.CornerStyle=="方角";frame.CornerRadius=new CornerRadius(square?0:16);titlePlate.CornerRadius=square?new CornerRadius(0):new CornerRadius(14,14,6,6);
        frame.Background=Appearance.FrameBackground(Group,Group.BackgroundOpacity*Group.Opacity);
        heading.Foreground=Ui.Brush(Group.LabelColor);heading.FontSize=HeaderFont;heading.FontFamily=new FontFamily(String.IsNullOrWhiteSpace(Group.LabelFont)?"Microsoft YaHei UI":Group.LabelFont);heading.TextWrapping=TextWrapping.NoWrap;heading.TextTrimming=TextTrimming.CharacterEllipsis;UpdateTitle();
        RefreshSectionTabs();
        tabsScroll.Visibility=Group.Rolled&&controller.Store.Data.RollupEnabled&&!hoverExpanded?Visibility.Collapsed:Visibility.Visible;
        bool canUp=portal!=null&&(transientPortal||!String.Equals(portal.Current,portal.Root,StringComparison.OrdinalIgnoreCase));
        backIcon.Visibility=previewPath!=null||canUp?Visibility.Visible:Visibility.Collapsed;
        if(previewPath!=null){if(File.Exists(previewPath))return;ClosePreview();}
        Group viewed=ViewGroup;string[] items=SearchItems(viewed);
        string signature=String.Join("\n",items.Select(p=>p+"|"+Thumbnails.Revision(p)))+"|"+controller.Search+"|"+viewed.Id+"|"+viewed.ViewMode+"|"+viewed.IconSize+"|"+controller.Store.Data.ShowFileExtensions+"|"+Width+"|"+(portal==null?"":portal.Current)+"|"+canUp;
        if(viewed.ViewMode=="详细信息")signature+="|"+String.Join(";",items.Select(p=>File.GetLastWriteTime(p).Ticks));
        if(signature==lastSignature){Thumbnails.RequestVisible(scroll);return;}lastSignature=signature;tiles.Children.Clear();fileNames.Clear();
        foreach(string path in items)tiles.Children.Add(MakeTile(path));
        if(items.Length==0){var text=Ui.Text(IsFolderPanel?"此文件夹为空":viewed.Kind=="应用"?"暂时没有匹配的桌面文件":"拖入文件或快捷方式",12,Ui.DesktopMuted);text.Margin=new Thickness(12,20,12,12);tiles.Children.Add(text);}
    }
    void ClosePreview(){if(previewPath==null)return;previewPath=null;scroll.Content=tiles;lastSignature=null;Refresh();}
    void NavigateBack(){if(portal==null)return;if(String.Equals(portal.Current,portal.Root,StringComparison.OrdinalIgnoreCase)&&transientPortal){portal=null;transientPortal=false;}else portal.Parent();lastSignature=null;Refresh();}
    void SelectGroup(Group selected){if(selected==null)return;previewPath=null;scroll.Content=tiles;Group.ActiveGroupId=selected.Id;EnsureSessions();lastSignature=null;controller.Store.Save();Refresh();}
    // 顶栏标签：自身、文件夹映射、合并分区都用同一个构造函数，只有内容来源不同。
    void RefreshSectionTabs(){
        if(editingTab)return;
        var tabs=PartitionTabs.Of(controller.Store.Data,Group);
        string signature=String.Join("|",tabs.Select(t=>t.Key+"@"+t.Title+"@"+(t.Active?1:0)+"@"+(t.Pinned?1:0)))+"#"+Width+"#"+Group.Color;
        if(signature==lastTabsSignature)return;
        lastTabsSignature=signature;
        sectionTabs.Children.Clear();
        int maxLabel=Math.Max(56,Math.Min(168,(int)(Math.Max(120,Width-176)/Math.Max(1,tabs.Count))-26));
        foreach(var tab in tabs)sectionTabs.Children.Add(MakeSectionTab(tab,maxLabel));
        var add=new TextBlock(){Text="＋",FontSize=17,Foreground=Ui.DesktopMuted,Cursor=Cursors.Hand,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(2,0,4,0),ToolTip="添加文件夹标签或新建分区标签"};
        add.MouseLeftButtonDown+=delegate(object s,MouseButtonEventArgs e){
            e.Handled=true;
            var menu=new ContextMenu();
            menu.Items.Add(Ui.Menu("添加文件夹标签…",AddFolderDialog));
            menu.Items.Add(Ui.Menu("新建分区标签…",NewSectionTabDialog));
            add.ContextMenu=menu;menu.PlacementTarget=add;menu.IsOpen=true;
        };
        sectionTabs.Children.Add(add);
    }
    Border MakeSectionTab(PartitionTab info,int maxLabel){
        Color tint=((SolidColorBrush)Ui.Brush(Group.Color)).Color;
        Brush activePlate=new SolidColorBrush(Color.FromArgb(96,246,250,255));
        Brush activeEdge=new SolidColorBrush(Color.FromArgb(200,tint.R,tint.G,tint.B));
        Brush hoverPlate=new SolidColorBrush(Color.FromArgb(34,255,255,255));
        var label=new TextBlock(){Text=info.Title,FontSize=HeaderFont,Foreground=info.Active?Ui.DesktopInk:Ui.DesktopMuted,FontWeight=info.Active?FontWeights.SemiBold:FontWeights.Normal,VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis,MaxWidth=maxLabel,TextWrapping=TextWrapping.NoWrap};
        var row=new StackPanel(){Orientation=Orientation.Horizontal};
        row.Children.Add(label);
        var closeGlyph=new TextBlock(){Text="×",FontSize=16,Foreground=Ui.DesktopMuted,Width=20,TextAlignment=TextAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
        var close=new Border(){Child=closeGlyph,Width=22,Height=22,CornerRadius=new CornerRadius(11),Background=Brushes.Transparent,Opacity=0,IsHitTestVisible=false,Margin=new Thickness(5,0,0,0),ToolTip=info.IsSelf?"删除此分区并把图标放回桌面；可撤销":"关闭此文件夹标签"};
        close.MouseEnter+=delegate{close.Background=new SolidColorBrush(Color.FromArgb(40,255,255,255));};
        close.MouseLeave+=delegate{close.Background=Brushes.Transparent;};
        close.MouseLeftButtonDown+=delegate(object s,MouseButtonEventArgs e){e.Handled=true;CloseSectionTab(info);};
        row.Children.Add(close);
        string hint=info.IsSelf?"双击改名；拖动移动整个分区":info.IsFolder?"拖动到别的分区合并，拖到桌面拆成独立分区":"拖动到别的分区合并，拖到桌面恢复独立分区";
        var tab=new Border(){Child=row,Cursor=Cursors.Hand,Padding=new Thickness(10,4,7,4),Margin=new Thickness(0,0,3,0),CornerRadius=new CornerRadius(9,9,2,2),Background=info.Active?activePlate:Brushes.Transparent,BorderBrush=info.Active?activeEdge:Brushes.Transparent,BorderThickness=info.Active?new Thickness(1,1,1,2):new Thickness(1,1,1,0),ToolTip=info.ToolTip+"\n"+hint};
        tab.MouseEnter+=delegate{if(!info.Active)tab.Background=hoverPlate;close.Opacity=info.Pinned?0:1;close.IsHitTestVisible=!info.Pinned;};
        tab.MouseLeave+=delegate{if(!info.Active)tab.Background=Brushes.Transparent;close.Opacity=0;close.IsHitTestVisible=false;};
        Point press=new Point(),pressScreen=new Point();double startLeft=0,startTop=0;
        bool held=false,dragging=false;
        tab.MouseLeftButtonDown+=delegate(object s,MouseButtonEventArgs e){
            if(e.OriginalSource==close)return;
            if(e.ClickCount==2&&!info.IsFolder){e.Handled=true;EditTabName(info.Owner,tab);return;}
            if(e.ClickCount!=1)return;
            press=e.GetPosition(tab);pressScreen=PointToScreen(e.GetPosition(this));startLeft=Left;startTop=Top;
            held=true;dragging=false;tab.CaptureMouse();e.Handled=true;
        };
        tab.MouseMove+=delegate(object s,MouseEventArgs e){
            if(!held)return;
            if(e.LeftButton!=MouseButtonState.Pressed){held=false;dragging=false;tab.ReleaseMouseCapture();tab.Opacity=1;return;}
            Point now=e.GetPosition(tab);
            if(!dragging&&(Math.Abs(now.X-press.X)+Math.Abs(now.Y-press.Y)>=10)){dragging=true;tab.Opacity=.65;}
            if(dragging&&info.IsSelf&&!Group.Locked){
                Point current=PointToScreen(e.GetPosition(this));double scale=Shell.WindowScale(this);
                Left=startLeft+(current.X-pressScreen.X)/scale;Top=startTop+(current.Y-pressScreen.Y)/scale;
            }
        };
        tab.MouseLeftButtonUp+=delegate(object s,MouseButtonEventArgs e){
            if(!held)return;
            Point drop=PointToScreen(e.GetPosition(this));
            bool wasDragging=dragging;held=false;dragging=false;tab.ReleaseMouseCapture();tab.Opacity=1;
            if(!wasDragging){ActivateSectionTab(info);e.Handled=true;return;}
            if(info.IsSelf){if(!Selected&&controller.TryMergeGroup(Group,drop,this)){e.Handled=true;return;}SaveBounds();e.Handled=true;return;}
            Point local=PointFromScreen(drop);
            if(local.X>=0&&local.X<=ActualWidth&&local.Y>=0&&local.Y<=GroupArrangement.HeaderHeight)ActivateSectionTab(info);
            else controller.DropPartitionTab(info,drop);
            e.Handled=true;
        };
        tab.LostMouseCapture+=delegate{held=false;dragging=false;tab.Opacity=1;};
        var menu=new ContextMenu();
        menu.Items.Add(Ui.Menu("切换到此标签",delegate{ActivateSectionTab(info);}));
        if(info.IsFolder){
            menu.Items.Add(Ui.Menu("在资源管理器中打开",delegate{Shell.Open(info.Folder);}));
            menu.Items.Add(Ui.Menu("拆成独立分区",delegate{controller.DetachFolderTab(Group,info.Owner,info.Folder,PointToScreen(new Point(Group.X+32,Group.Y+GroupArrangement.HeaderHeight)));}));
            menu.Items.Add(Ui.Menu("关闭标签（只移除映射）",delegate{CloseSectionTab(info);}));
        }else if(info.IsMember){
            menu.Items.Add(Ui.Menu("重命名标签",delegate{EditTabName(info.Owner,tab);}));
            menu.Items.Add(Ui.Menu(info.Pinned?"取消固定":"固定标签",delegate{controller.Store.Checkpoint();info.Owner.TabPinned=!info.Owner.TabPinned;controller.Store.Save();lastTabsSignature=null;Refresh();}));
            if(!info.Pinned)menu.Items.Add(Ui.Menu("关闭标签并恢复独立分区",delegate{CloseSectionTab(info);}));
        }else menu.Items.Add(Ui.Menu("重命名分区",delegate{EditTabName(Group,tab);}));
        tab.ContextMenu=menu;
        return tab;
    }
    void ActivateSectionTab(PartitionTab info){
        if(info==null)return;
        ClosePreview();scroll.Content=tiles;
        if(info.IsFolder){
            if(info.Owner.Folders==null)info.Owner.Folders=new List<string>();
            int index=info.Owner.Folders.FindIndex(f=>String.Equals(f,info.Folder,StringComparison.OrdinalIgnoreCase));
            if(index<0){info.Owner.Folders.Add(info.Folder);index=info.Owner.Folders.Count-1;}
            bool same=ViewGroup==info.Owner&&info.Owner.FolderIndex==index;
            Group.ActiveGroupId=info.Owner==Group?Group.Id:info.Owner.Id;
            info.Owner.FolderIndex=index;transientPortal=false;
            EnsureSessions();
            if(same&&portal!=null&&!String.Equals(portal.Current,portal.Root,StringComparison.OrdinalIgnoreCase)){portal=new PortalSession(info.Folder);sessions[index]=portal;}
        }else{
            Group.ActiveGroupId=info.Owner.Id;
            if(info.IsSelf&&Group.Folders.Count>0)Group.FolderIndex=-1;
            transientPortal=false;
            EnsureSessions();
        }
        lastSignature=null;lastTabsSignature=null;
        controller.Store.Save();Refresh();
    }
    void CloseSectionTab(PartitionTab info){
        if(info==null)return;
        if(info.IsSelf){
            if(controller.Manager==null)return;
            // 标签上的 ×：删除分区并把项目放回桌面；等点击处理完再重建，避免在事件里关掉自己。
            Dispatcher.BeginInvoke(new Action(delegate{if(controller.Manager!=null)controller.Manager.DeleteGroup(Group);}));
            return;
        }
        if(info.IsMember){if(!info.Pinned)controller.DetachGroup(Group,info.Owner);return;}
        if(info.Owner==null)return;
        int index=info.Owner.Folders.FindIndex(f=>String.Equals(f,info.Folder,StringComparison.OrdinalIgnoreCase));
        if(index>=0)RemoveFolder(info.Owner,index);
    }
    void NewSectionTabDialog(){
        if(controller.Manager==null)return;
        string name=Ui.Ask("新建分区标签","标签名称","新分区",this);
        if(String.IsNullOrWhiteSpace(name))return;
        controller.NewSectionTab(Group,name);lastTabsSignature=null;Refresh();
    }
    void EditTabName(Group target,Border tab){if(editingTab||target==null)return;editingTab=true;var label=tab.Child;var editor=new TextBox(){Text=target.Name,MinWidth=80,MaxWidth=150,FontSize=HeaderFont};tab.Child=editor;bool done=false;Action<bool> finish=delegate(bool save){if(done)return;done=true;if(save&&!String.IsNullOrWhiteSpace(editor.Text)&&editor.Text.Trim()!=target.Name){controller.Store.Checkpoint();target.Name=editor.Text.Trim();controller.Store.Save();}tab.Child=label;editingTab=false;lastTabsSignature=null;Refresh();};editor.KeyDown+=delegate(object s,KeyEventArgs e){if(e.Key==Key.Enter){finish(true);e.Handled=true;}else if(e.Key==Key.Escape){finish(false);e.Handled=true;}};editor.LostKeyboardFocus+=delegate{finish(true);};editor.Focus();editor.SelectAll();}
    bool ShowPreview(string path){
        string ext=Path.GetExtension(path).ToLowerInvariant();bool picture=new[]{".png",".jpg",".jpeg",".gif",".bmp",".tif",".tiff",".webp"}.Contains(ext);
        bool textFile=new[]{".txt",".md",".csv",".log",".json",".xml",".cs",".ps1",".ini"}.Contains(ext);
        if(!picture&&!textFile)return false;
        try{
            var panel=new DockPanel(){Margin=new Thickness(10)};
            var bar=new DockPanel(){Margin=new Thickness(0,0,0,8)};DockPanel.SetDock(bar,Dock.Top);
            var external=Ui.Button("用其他程序打开",delegate{Shell.Open(path);});DockPanel.SetDock(external,Dock.Right);bar.Children.Add(external);
            bar.Children.Add(Ui.Text(Path.GetFileName(path),12,Ui.DesktopInk));panel.Children.Add(bar);
            if(picture){var bitmap=new BitmapImage();bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.UriSource=new Uri(path,UriKind.Absolute);bitmap.EndInit();bitmap.Freeze();panel.Children.Add(new Image(){Source=bitmap,Stretch=Stretch.Uniform,HorizontalAlignment=HorizontalAlignment.Stretch,VerticalAlignment=VerticalAlignment.Top});}
            else{if(new FileInfo(path).Length>1024*1024)return false;panel.Children.Add(new TextBox(){Text=File.ReadAllText(path),IsReadOnly=true,TextWrapping=TextWrapping.Wrap,Background=Brushes.Transparent,Foreground=Ui.DesktopInk,BorderThickness=new Thickness(0),VerticalScrollBarVisibility=ScrollBarVisibility.Auto});}
            previewPath=path;scroll.Content=panel;backIcon.Visibility=Visibility.Visible;scroll.ScrollToTop();return true;
        }catch{return false;}
    }

    string FolderLabel(string path){string name=Path.GetFileName((path??"").TrimEnd(Path.DirectorySeparatorChar));return String.IsNullOrEmpty(name)?(path??""):name;}
    void EnsureSessions(){
        var folders=FolderGroup;
        sessions.Clear();foreach(string folder in folders.Folders)sessions.Add(new PortalSession(folder));
        if(folders.FolderIndex>=sessions.Count)folders.FolderIndex=sessions.Count==0?0:sessions.Count-1;
        if(folders.FolderIndex<0&&folders.Kind=="文件夹"&&!String.IsNullOrEmpty(folders.Folder))folders.FolderIndex=0;
        portal=ViewGroup!=folders||folders.FolderIndex<0||sessions.Count==0?null:sessions[folders.FolderIndex];transientPortal=false;
    }


    void AddFolderDialog(){if(controller.Manager==null)return;using(var dialog=new System.Windows.Forms.FolderBrowserDialog()){dialog.Description="选择要在面板中显示的文件夹";if(dialog.ShowDialog()!=System.Windows.Forms.DialogResult.OK)return;AddFolders(new[]{dialog.SelectedPath});}}
    void AddFolders(string[] paths){
        if(paths==null||paths.Length==0)return;var folders=FolderGroup;var added=new List<string>();foreach(string path in paths){string full;try{full=Path.GetFullPath(path);}catch{continue;}if(!Directory.Exists(full))continue;if(folders.Folders.Any(f=>String.Equals(f,full,StringComparison.OrdinalIgnoreCase))||added.Any(f=>String.Equals(f,full,StringComparison.OrdinalIgnoreCase)))continue;added.Add(full);}
        if(added.Count==0)return;ClosePreview();controller.Store.Checkpoint();folders.Folders.AddRange(added);folders.FolderIndex=folders.Folders.Count-added.Count;if(folders==Group)Group.ActiveGroupId=Group.Id;EnsureSessions();lastSignature=null;controller.SaveAndRefresh(false);Refresh();
    }
    void RemoveFolder(Group owner,int index){controller.CloseFolderTab(owner,index);lastSignature=null;lastTabsSignature=null;Refresh();}
    string lastSignature=null;
    Border MakeTile(string path){
        bool listMode=ViewGroup.ViewMode!="图标";var box=new Border(){Width=listMode?Math.Max(170,Width-38):Math.Max(88,ViewGroup.IconSize+36),Height=listMode?(ViewGroup.ViewMode=="详细信息"?54:38):ViewGroup.IconSize+61,Margin=new Thickness(2),CornerRadius=new CornerRadius(8),Background=Brushes.Transparent,Tag="qidesk-file",Cursor=Cursors.Hand};
        box.ToolTip=Path.GetFileName(path)+"\n"+FilePresentation.Detail(path);box.BorderThickness=new Thickness(1);box.BorderBrush=Brushes.Transparent;
        var stack=new StackPanel(){Margin=new Thickness(4,8,4,0)};double size=listMode?24:ViewGroup.IconSize;bool application=FilePresentation.IsApplication(path);
        FrameworkElement visual=FileTiles.Plate(path,ViewGroup,size,listMode,controller.Store.Data);
        var name=new TextBlock(){Text=FilePresentation.DisplayName(path,controller.Store.Data.ShowFileExtensions),Foreground=Ui.DesktopInk,FontSize=application?11.5:11,TextAlignment=listMode?TextAlignment.Left:TextAlignment.Center,TextWrapping=listMode?TextWrapping.NoWrap:TextWrapping.Wrap,MaxHeight=34,TextTrimming=TextTrimming.CharacterEllipsis,Effect=Ui.Shadow(1,3,.8)};
        var fileName=FileNameLabel(name,path,box);
        if(listMode){var row=new DockPanel(){Margin=new Thickness(7,4,7,4)};DockPanel.SetDock(visual,Dock.Left);row.Children.Add(visual);var detail=new StackPanel(){VerticalAlignment=VerticalAlignment.Center};detail.Children.Add(fileName);if(ViewGroup.ViewMode=="详细信息")detail.Children.Add(new TextBlock(){Text=FilePresentation.Detail(path),FontSize=10,Foreground=Ui.DesktopMuted,TextTrimming=TextTrimming.CharacterEllipsis});row.Children.Add(detail);box.Child=row;}else{stack.Children.Add(visual);stack.Children.Add(fileName);box.Child=stack;}
        Point press=new Point();bool held=false;
        box.MouseEnter+=delegate{box.Background=new SolidColorBrush(Color.FromArgb(70,110,142,184));box.BorderBrush=new SolidColorBrush(Color.FromArgb(110,232,241,255));};
        box.MouseLeave+=delegate{box.Background=Brushes.Transparent;box.BorderBrush=Brushes.Transparent;};
        box.MouseLeftButtonDown+=delegate(object s,MouseButtonEventArgs e){if(fileNameEditor!=null)return;selectedFile=path;box.Focusable=true;box.Focus();if(e.ClickCount==2){held=false;box.ReleaseMouseCapture();if(Directory.Exists(path)){if(portal==null){portal=new PortalSession(path);transientPortal=true;}else portal.Navigate(path);lastSignature=null;Refresh();}else if(!ShowPreview(path))Shell.Open(path);e.Handled=true;return;}press=e.GetPosition(box);held=true;box.CaptureMouse();e.Handled=true;};
        box.MouseLeftButtonUp+=delegate{held=false;box.ReleaseMouseCapture();};
        box.MouseMove+=delegate(object s,MouseEventArgs e){if(!held||e.LeftButton!=MouseButtonState.Pressed)return;Point p=e.GetPosition(box);if(Math.Abs(p.X-press.X)+Math.Abs(p.Y-press.Y)<10)return;held=false;box.ReleaseMouseCapture();bool desktopBound=DesktopItemDrag.IsDesktopFile(path)||controller.Store.Data.ManagedDesktopItems.ContainsKey(path);var data=desktopBound?Functions.LogicalDrag(new[]{path}):new DataObject(DataFormats.FileDrop,new[]{path});bool cancelled=false;QueryContinueDragEventHandler cancellation=delegate(object sender,QueryContinueDragEventArgs args){if(args.EscapePressed)cancelled=true;};box.QueryContinueDrag+=cancellation;try{DragDrop.DoDragDrop(box,data,controller.Store.Data.ManagedDesktopItems.ContainsKey(path)?DragDropEffects.Link|DragDropEffects.Move:DragDropEffects.Link|DragDropEffects.Copy|DragDropEffects.Move);}finally{box.QueryContinueDrag-=cancellation;}DesktopItemDrag.Complete(controller,ViewGroup,path,cancelled);};
        box.ContextMenu=new ContextMenu();box.ContextMenuOpening+=delegate(object sender,ContextMenuEventArgs e){e.Handled=true;held=false;if(controller.Manager==null)return;Point point=e.CursorLeft<0?box.PointToScreen(new Point(12,12)):PointToScreen(Mouse.GetPosition(this));controller.Manager.ShowFileMenu(this,path,ViewGroup,point,false,delegate{lastSignature=null;Refresh();});};

        box.AllowDrop=true;box.DragOver+=delegate(object sender,DragEventArgs e){e.Effects=Directory.Exists(path)?DragTransfer.Effect(e,path):DragDropEffects.None;e.Handled=true;};box.Drop+=delegate(object sender,DragEventArgs e){e.Handled=true;if(Directory.Exists(path))DragTransfer.Drop(this,controller,e,path,delegate{lastSignature=null;Refresh();});};return box;
    }
    public bool PlaceOnDesktop(IntPtr host){Docked=Shell.Attach(this,host);return Docked;}
    // Ctrl＋左键多选：选中的分区加粗高亮，Enter 可一起置顶或收起。
    public bool Selected{get{return controller.SelectedGroups.Contains(Group.Id);}}
    public void RefreshSelection(){ApplySelectionVisual();}
    void ApplySelectionVisual(){
        bool selected=Selected;
        frame.BorderThickness=new Thickness(selected?3:1.5);
        if(selected)frame.BorderBrush=Ui.Accent;
        if(selectGlyph!=null){selectGlyph.Text=selected?"●":"◯";selectGlyph.Foreground=selected?Ui.Brush(Group.Color):Ui.DesktopInk;}
    }
}
}
