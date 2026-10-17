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
public class FenceWindow : Window {
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
    bool transferPageArmed;
    bool hoverExpanded;
    PortalSession portal;
    bool transientPortal;
    string previewPath;
    List<PortalSession> sessions=new List<PortalSession>();
    StackPanel tabBar;
    ScrollViewer tabsScroll;
    StackPanel workTabs;
    StackPanel groupTabs;
    ScrollViewer groupTabsScroll;
    TextBlock backIcon;
    Border ignoreTarget;
    Border selectTarget,unpinTarget,deleteTarget;
    TextBlock selectGlyph;
    bool editingTab;
    StackPanel headerItems;
    int HeaderFont {get{return Math.Max(17,(int)Math.Round(Group.LabelSize));}}
    Group ViewGroup {get{var selected=controller.Store.Data.Groups.FirstOrDefault(g=>g.Id==Group.ActiveGroupId&&g.HostGroupId==Group.Id);return selected??Group;}}
    Group FolderGroup {get{return Group.Folders.Count>0?Group:ViewGroup;}}
    bool IsFolderPanel {get{return FolderGroup.Kind=="文件夹"||FolderGroup.Folders.Count>0;}}
    public bool Docked;
    public FenceWindow(Controller owner,Group group) {
        controller=owner;Group=group;
        Title=group.Name;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;
        AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;ShowActivated=false;
        FontFamily=new FontFamily("Microsoft YaHei UI");Foreground=Ui.DesktopInk;MinWidth=220;MinHeight=40;
        frame=new Border(){CornerRadius=new CornerRadius(16),BorderThickness=new Thickness(1.5),BorderBrush=Ui.Brush(group.Color),Background=Brushes.Transparent,Effect=Ui.Shadow(3,12,.30)};
        var grid=new Grid();grid.RowDefinitions.Add(new RowDefinition(){Height=new GridLength(42)});grid.RowDefinitions.Add(new RowDefinition(){Height=GridLength.Auto});grid.RowDefinitions.Add(new RowDefinition(){Height=new GridLength(1,GridUnitType.Star)});
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
        groupTabs=new StackPanel(){Orientation=Orientation.Horizontal,Margin=new Thickness(8,0,0,0)};
        groupTabsScroll=new ScrollViewer(){Content=groupTabs,HorizontalScrollBarVisibility=ScrollBarVisibility.Hidden,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,MaxWidth=220};
        groupTabsScroll.PreviewMouseWheel+=delegate(object s,MouseWheelEventArgs e){groupTabsScroll.ScrollToHorizontalOffset(groupTabsScroll.HorizontalOffset-Math.Sign(e.Delta)*90);e.Handled=true;};
        headerItems.Children.Add(groupTabsScroll);header.Children.Add(headerItems);
        var ignoreGlyph=Ui.Text("⊘",17,Ui.DesktopMuted);ignoreGlyph.Margin=new Thickness(0);ignoreGlyph.VerticalAlignment=VerticalAlignment.Center;ignoreGlyph.HorizontalAlignment=HorizontalAlignment.Center;
        ignoreTarget=new Border(){Child=ignoreGlyph,Width=28,Height=28,Background=Brushes.Transparent,BorderBrush=Ui.Brush(group.Color),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(7),AllowDrop=true,Cursor=Cursors.Hand,ToolTip="拖入项目，忽略当前分区或文件夹标签",Opacity=0,IsHitTestVisible=false};
        selectGlyph=Ui.Text("○",18,Ui.DesktopMuted);selectGlyph.HorizontalAlignment=HorizontalAlignment.Center;selectGlyph.VerticalAlignment=VerticalAlignment.Center;
        selectTarget=new Border(){Child=selectGlyph,Width=28,Height=28,Background=Brushes.Transparent,CornerRadius=new CornerRadius(7),Cursor=Cursors.Hand,ToolTip="选择此工作区；Ctrl＋左键可多选",Opacity=0,IsHitTestVisible=false};
        selectTarget.MouseLeftButtonDown+=delegate(object s,MouseButtonEventArgs e){e.Handled=true;controller.ToggleGroupSelection(Group);};
        Grid.SetColumn(selectTarget,1);header.Children.Add(selectTarget);
        var unpinGlyph=Ui.Text("⌖",17,Ui.DesktopMuted);unpinGlyph.HorizontalAlignment=HorizontalAlignment.Center;unpinGlyph.VerticalAlignment=VerticalAlignment.Center;
        unpinTarget=new Border(){Child=unpinGlyph,Width=28,Height=28,Background=Brushes.Transparent,CornerRadius=new CornerRadius(7),Cursor=Cursors.Hand,ToolTip="从工作区选择中移除；桌面分区保留",Opacity=0,IsHitTestVisible=false};
        unpinTarget.MouseLeftButtonDown+=delegate(object s,MouseButtonEventArgs e){e.Handled=true;if(Selected)controller.ToggleGroupSelection(Group);};
        Grid.SetColumn(unpinTarget,2);header.Children.Add(unpinTarget);
        Grid.SetColumn(ignoreTarget,3);header.Children.Add(ignoreTarget);
        var deleteGlyph=Ui.Text("×",21,Ui.DesktopMuted);deleteGlyph.HorizontalAlignment=HorizontalAlignment.Center;deleteGlyph.VerticalAlignment=VerticalAlignment.Center;
        deleteTarget=new Border(){Child=deleteGlyph,Width=28,Height=28,Background=Brushes.Transparent,CornerRadius=new CornerRadius(7),Cursor=Cursors.Hand,ToolTip="删除此分区；可在管理面板撤销",Opacity=0,IsHitTestVisible=false};
        deleteTarget.MouseLeftButtonDown+=delegate(object s,MouseButtonEventArgs e){e.Handled=true;if(controller.Manager!=null)controller.Manager.DeleteTabGroup(Group);};
        Grid.SetColumn(deleteTarget,4);header.Children.Add(deleteTarget);
        Action<bool> showHeaderActions=delegate(bool visible){foreach(var target in new[]{selectTarget,unpinTarget,ignoreTarget,deleteTarget}){target.Opacity=visible?1:0;target.IsHitTestVisible=visible;}};
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
            controller.Store.Checkpoint();moving=true;transferPageArmed=false;dragStart=PointToScreen(e.GetPosition(this));startX=Left;startY=Top;header.CaptureMouse();e.Handled=true;
        };
        header.MouseMove+=delegate(object s,MouseEventArgs e){if(!moving)return;Point p=PointToScreen(e.GetPosition(this));var source=PresentationSource.FromVisual(this);double scale=source==null?1:source.CompositionTarget.TransformToDevice.M11;Left=startX+(p.X-dragStart.X)/scale;Top=startY+(p.Y-dragStart.Y)/scale;if(e.RightButton==MouseButtonState.Pressed)transferPageArmed=true;int destination=GroupPages.Destination(Group.Page,controller.Store.Data.Pages,Left-startX,transferPageArmed);if(destination!=Group.Page)heading.Text=Group.Name+"  →  第 "+(destination+1)+" 页";};
        header.MouseRightButtonDown+=delegate(object s,MouseButtonEventArgs e){if(moving){transferPageArmed=true;e.Handled=true;}};
        header.MouseLeftButtonUp+=delegate(object s,MouseButtonEventArgs e){if(!moving)return;Point drop=PointToScreen(e.GetPosition(this));moving=false;header.ReleaseMouseCapture();if(Math.Abs(Left-startX)+Math.Abs(Top-startY)<6){if(e.OriginalSource==heading&&Group.ActiveGroupId!=Group.Id)SelectGroup(Group);return;}if(controller.TryMergeGroup(Group,drop,this))return;if(!controller.TransferGroupPage(Group,Left-startX,transferPageArmed,new Point(Left,Top),Shell.WorkingAreas(this)))SaveBounds();};
        scroll=new ScrollViewer(){Content=tiles,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Margin=new Thickness(6,0,6,12)};
        scroll.Loaded+=delegate{Ui.SetVerticalScrollBarOpacity(scroll,scroll.IsMouseOver?1:0);};
        scroll.MouseEnter+=delegate{Ui.SetVerticalScrollBarOpacity(scroll,1);};
        scroll.MouseLeave+=delegate{Ui.SetVerticalScrollBarOpacity(scroll,0);};
        titlePlate=new Border(){Child=header,CornerRadius=new CornerRadius(14,14,6,6),Margin=new Thickness(3),Background=new SolidColorBrush(Color.FromArgb(100,18,27,42))};
        Grid.SetRow(scroll,2);grid.Children.Add(titlePlate);grid.Children.Add(scroll);
        tabBar=new StackPanel(){Orientation=Orientation.Horizontal};
        {
            EnsureSessions();tabsScroll=new ScrollViewer(){Content=tabBar,HorizontalScrollBarVisibility=ScrollBarVisibility.Hidden,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,Margin=new Thickness(10,0,4,0),MaxWidth=420,Visibility=IsFolderPanel?Visibility.Visible:Visibility.Collapsed};
            tabsScroll.PreviewMouseWheel+=delegate(object s,MouseWheelEventArgs e){tabsScroll.ScrollToHorizontalOffset(tabsScroll.HorizontalOffset-Math.Sign(e.Delta)*90);e.Handled=true;};
            tabsScroll.AllowDrop=true;
            tabsScroll.DragOver+=delegate(object s,DragEventArgs e){e.Effects=Functions.DragPaths(e.Data)!=null?DragDropEffects.Link:DragDropEffects.None;e.Handled=true;};
            tabsScroll.Drop+=delegate(object s,DragEventArgs e){e.Handled=true;AddFolders(Functions.DragPaths(e.Data));};
            headerItems.Children.Add(tabsScroll);
            headerItems.Children.Remove(groupTabsScroll);headerItems.Children.Add(groupTabsScroll);
        }
        workTabs=new StackPanel(){Orientation=Orientation.Horizontal,Margin=new Thickness(10,0,0,0)};headerItems.Children.Add(workTabs);
        header.AllowDrop=true;
        header.DragOver+=delegate(object s,DragEventArgs e){var paths=Functions.DragPaths(e.Data);e.Effects=paths!=null&&paths.Any(Directory.Exists)?DragDropEffects.Link:DragDropEffects.None;e.Handled=true;};
        header.Drop+=delegate(object s,DragEventArgs e){e.Handled=true;AddFolders(Functions.DragPaths(e.Data));};
        resize=AddResizeHandle(grid,HorizontalAlignment.Right,16,Cursors.SizeNWSE,1,group.Color);
        AddResizeHandle(grid,HorizontalAlignment.Left,16,Cursors.SizeNESW,-1,group.Color);
        AddResizeHandle(grid,HorizontalAlignment.Center,64,Cursors.SizeNS,0,group.Color);
        frame.Child=grid;Content=frame;AllowDrop=true;
        PreviewMouseWheel+=delegate(object sender,MouseWheelEventArgs e){if((Keyboard.Modifiers&ModifierKeys.Control)==0)return;e.Handled=true;string before=ViewGroup.ViewMode+ViewGroup.IconSize;controller.Store.Checkpoint();int steps=Math.Max(1,Math.Abs(e.Delta)/120);for(int i=0;i<Math.Min(steps,10);i++)FilePresentation.Zoom(ViewGroup,Math.Sign(e.Delta));if(before!=ViewGroup.ViewMode+ViewGroup.IconSize){lastSignature=null;controller.SaveAndRefresh(false);}};
        PreviewKeyDown+=delegate(object sender,KeyEventArgs e){Key key=e.Key==Key.System?e.SystemKey:e.Key;if(Selected){if(key==Key.Enter){controller.PopOutSelectedGroups();e.Handled=true;return;}if(key==Key.Escape){controller.ClearGroupSelection();e.Handled=true;return;}}if(portal!=null&&(Keyboard.Modifiers&ModifierKeys.Alt)!=0&&(key==Key.Left||key==Key.Up)){if(key==Key.Left){portal.Back();lastSignature=null;Refresh();}else NavigateBack();e.Handled=true;return;}if(e.Key==Key.F5){lastSignature=null;Refresh();e.Handled=true;}};
        DragOver+=delegate(object s,DragEventArgs e){e.Effects=portal!=null?DragTransfer.Effect(e,portal.Current):Functions.DragPaths(e.Data)!=null&&(e.AllowedEffects&DragDropEffects.Link)!=0?DragDropEffects.Link:DragDropEffects.None;e.Handled=true;};
        Drop+=delegate(object s,DragEventArgs e){if(portal!=null){DragTransfer.Drop(this,controller,e,portal.Current,delegate{lastSignature=null;Refresh();});return;}var paths=Functions.DragPaths(e.Data);if(paths==null)return;controller.Store.Checkpoint();foreach(string path in paths)controller.Store.Assign(path,ViewGroup);controller.SaveAndRefresh();e.Handled=true;};
        ContextMenu=new ContextMenu();
        ContextMenuOpening+=delegate(object sender,ContextMenuEventArgs e){e.Handled=true;if(moving||controller.Manager==null)return;Point point=e.CursorLeft<0?PointToScreen(new Point(24,24)):PointToScreen(Mouse.GetPosition(this));controller.Manager.ShowFileMenu(this,FilePresentation.OpenFolder(ViewGroup,portal==null?ViewGroup.Folder:portal.Current),ViewGroup,point,true,delegate{lastSignature=null;Refresh();});};
        bool scrollPending=false;scroll.ScrollChanged+=delegate{if(scrollPending)return;scrollPending=true;Dispatcher.BeginInvoke(new Action(delegate{scrollPending=false;Thumbnails.RequestVisible(scroll);}),System.Windows.Threading.DispatcherPriority.Background);};

        MouseEnter+=delegate{if(Group.Rolled&&controller.Store.Data.RollupEnabled&&controller.Store.Data.HoverExpand){hoverExpanded=true;ApplyBounds();}UpdateTitle();};
        MouseLeave+=delegate{if(hoverExpanded){hoverExpanded=false;ApplyBounds();}UpdateTitle();};
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
    List<IgnoreRule> CurrentIgnoreRules(){
        var viewed=ViewGroup;
        if(portal==null||viewed.FolderIndex<0||viewed.FolderIndex>=viewed.Folders.Count)return viewed.IgnoreRules;
        string folder=viewed.Folders[viewed.FolderIndex];List<IgnoreRule> rules;
        if(!viewed.FolderIgnoreRules.TryGetValue(folder,out rules)){rules=new List<IgnoreRule>();viewed.FolderIgnoreRules[folder]=rules;}
        return rules;
    }
    ContextMenu BuildIgnoreMenu(){
        var menu=new ContextMenu();var rules=CurrentIgnoreRules();var global=controller.Store.Data.IgnoreRules;
        menu.Items.Add(new MenuItem(){Header="全局规则 · "+global.Count+" 条",IsEnabled=false});
        foreach(var rule in global.Take(12).ToArray()){var current=rule;menu.Items.Add(Ui.Menu("取消全局忽略 · "+current.Pattern,delegate{global.Remove(current);controller.Store.Save();lastSignature=null;Refresh();}));}
        menu.Items.Add(new Separator());
        menu.Items.Add(new MenuItem(){Header="当前分区 · "+(portal==null?ViewGroup.Name:FolderLabel(ViewGroup.Folders[ViewGroup.FolderIndex]))+" · "+rules.Count+" 条",IsEnabled=false});
        foreach(var rule in rules.Take(12).ToArray()){var current=rule;menu.Items.Add(Ui.Menu("取消当前忽略 · "+current.Pattern,delegate{rules.Remove(current);controller.Store.Save();lastSignature=null;Refresh();}));}
        menu.Items.Add(new Separator());
        menu.Items.Add(Ui.Menu("编辑全部忽略规则与导入文件",delegate{if(controller.Manager!=null)controller.Manager.ShowIgnoreRules(ViewGroup);}));
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
        if(controller.Manager!=null)controller.Manager.SetStatus("已在当前标签忽略 "+pending.Count+" 项；点击右上角忽略图标可撤销。");
    }
    void RenameGroup(Group target){
        if(target==null)return;var label=target==Group?(TextBlock)heading:null;
        if(label==null)return;
        var editor=new TextBox(){Text=target.Name,MinWidth=80,MaxWidth=180,FontSize=14,Margin=new Thickness(0,4,0,0)};int index=headerItems.Children.IndexOf(label);headerItems.Children.RemoveAt(index);headerItems.Children.Insert(index,editor);
        bool done=false;Action<bool> finish=delegate(bool save){if(done)return;done=true;if(save&&!String.IsNullOrWhiteSpace(editor.Text)&&editor.Text.Trim()!=target.Name){controller.Store.Checkpoint();target.Name=editor.Text.Trim();controller.Store.Save();}headerItems.Children.Remove(editor);headerItems.Children.Insert(index,label);Refresh();};
        editor.KeyDown+=delegate(object s,KeyEventArgs e){if(e.Key==Key.Enter){finish(true);e.Handled=true;}else if(e.Key==Key.Escape){finish(false);e.Handled=true;}};editor.LostKeyboardFocus+=delegate{finish(true);};editor.Focus();editor.SelectAll();
    }
    void ToggleRoll(){if(!controller.Store.Data.RollupEnabled)return;controller.Store.Checkpoint();Group.Rolled=!Group.Rolled;hoverExpanded=false;ApplyBounds();controller.Store.Save();}
    void SaveBounds(){if(updating||Group.Locked)return;double oldX=Group.X,oldY=Group.Y,oldWidth=Group.Width,oldHeight=Group.Height;string oldEdge=Group.Edge;Group.X=Left;Group.Y=Top;Group.Edge="";Group.Width=Width;if(!Group.Rolled)Group.Height=Height;
        if(controller.Store.Data.SnapEnabled&&(Keyboard.Modifiers&ModifierKeys.Shift)==0)Layout.Snap(Group,controller.Store.Data.Groups.Where(g=>g.Id!=Group.Id&&g.Page==Group.Page),controller.Store.Data.SnapGrid,controller.Store.Data.SnapGap);
        if(controller.Store.Data.RollupEnabled&&controller.Store.Data.EdgeRollup)foreach(var area in Shell.WorkingAreas(this)){if(area.Contains(new Point(Group.X+40,Group.Y+20))){if(Math.Abs(Group.Y-area.Top)<14){Group.Edge="top";Group.Y=area.Top;Group.Rolled=true;}else if(Math.Abs(Group.Y+Height-area.Bottom)<14){Group.Edge="bottom";Group.Y=area.Bottom-42;Group.Rolled=true;}break;}}
        Point free;var obstacles=controller.Store.Data.Groups.Where(g=>g.Id!=Group.Id&&g.Page==Group.Page&&String.IsNullOrEmpty(g.HostGroupId)).Cast<Group>().Concat(controller.Store.Data.Widgets.Where(w=>w.Page==Group.Page));if(GroupArrangement.NearestFree(Group,obstacles,Shell.WorkingAreas(this),Math.Max(8,controller.Store.Data.SnapGap),out free)){Group.X=free.X;Group.Y=free.Y;}else{Group.X=oldX;Group.Y=oldY;Group.Width=oldWidth;Group.Height=oldHeight;Group.Edge=oldEdge;}
        ApplyBounds();controller.SaveAndRefresh(false);
    }
    void ApplyBounds(){
        updating=true;bool collapsed=Group.Rolled&&controller.Store.Data.RollupEnabled&&!hoverExpanded;
        Left=Group.X;Width=Group.Width;Height=collapsed?42:Group.Height;Top=Group.Edge=="bottom"&&!collapsed?Group.Y+42-Height:Group.Y;Opacity=1;
        double tabSpace=Math.Max(120,Group.Width-160);bool hasMembers=controller.Store.Data.Groups.Any(g=>g.HostGroupId==Group.Id);
        if(tabsScroll!=null){tabsScroll.MaxWidth=hasMembers?Math.Max(120,tabSpace*.62):tabSpace;tabsScroll.Visibility=collapsed?Visibility.Collapsed:Visibility.Visible;}
        if(groupTabsScroll!=null)groupTabsScroll.MaxWidth=hasMembers?Math.Max(100,tabSpace*.38):0;
        scroll.Visibility=collapsed?Visibility.Collapsed:Visibility.Visible;if(workTabs!=null)workTabs.Visibility=collapsed?Visibility.Collapsed:Visibility.Visible;
        updating=false;UpdateTitle();
    }
    void UpdateTitle(){bool tabs=controller.Store.Data.Groups.Any(g=>g.HostGroupId==Group.Id);heading.Visibility=(Group.Kind=="文件夹"&&String.IsNullOrEmpty(Group.Folder)||tabs)?Visibility.Collapsed:Group.TitleMode=="从不"?Visibility.Hidden:Group.TitleMode=="鼠标悬停"&&!IsMouseOver?Visibility.Hidden:Visibility.Visible;}
    public void Refresh(){
        heading.Text=Group.Name+(String.IsNullOrEmpty(Group.Folder)?"":"  ↗");Title=Group.Name;
        var edgeColor=((SolidColorBrush)Ui.Brush(Group.Color)).Color;frame.BorderBrush=new SolidColorBrush(Color.FromArgb((byte)Math.Round(255*Group.BorderOpacity),edgeColor.R,edgeColor.G,edgeColor.B));Opacity=1;ApplySelectionVisual();
        bool square=Group.CornerStyle=="方角";frame.CornerRadius=new CornerRadius(square?0:16);titlePlate.CornerRadius=square?new CornerRadius(0):new CornerRadius(14,14,6,6);
        frame.Background=Appearance.FrameBackground(Group,Group.BackgroundOpacity*Group.Opacity);
        heading.Foreground=Ui.Brush(Group.LabelColor);heading.FontSize=HeaderFont;heading.FontFamily=new FontFamily(String.IsNullOrWhiteSpace(Group.LabelFont)?"Microsoft YaHei UI":Group.LabelFont);heading.TextWrapping=TextWrapping.NoWrap;heading.TextTrimming=TextTrimming.CharacterEllipsis;UpdateTitle();
        RefreshGroupTabs();if(IsFolderPanel)RefreshTabs();
        tabsScroll.Visibility=Group.Rolled&&controller.Store.Data.RollupEnabled&&!hoverExpanded||!IsFolderPanel?Visibility.Collapsed:Visibility.Visible;
        workTabs.Visibility=ViewGroup.Kind=="工作"?Visibility.Visible:Visibility.Collapsed;if(workTabs.Visibility==Visibility.Visible)RefreshWorkTabs();
        bool canUp=portal!=null&&(transientPortal||!String.Equals(portal.Current,portal.Root,StringComparison.OrdinalIgnoreCase));
        backIcon.Visibility=previewPath!=null||canUp?Visibility.Visible:Visibility.Collapsed;
        if(previewPath!=null){if(File.Exists(previewPath))return;ClosePreview();}
        Group viewed=ViewGroup;string[] items=controller.Store.ItemsAt(viewed,controller.Search,portal==null?viewed.Folder:portal.Current).Take(1000).ToArray();
        string signature=String.Join("\n",items.Select(p=>p+"|"+Thumbnails.Revision(p)))+"|"+controller.Search+"|"+viewed.Id+"|"+viewed.ViewMode+"|"+viewed.IconSize+"|"+controller.Store.Data.ShowFileExtensions+"|"+Width+"|"+(portal==null?"":portal.Current)+"|"+canUp;
        if(viewed.ViewMode=="详细信息")signature+="|"+String.Join(";",items.Select(p=>File.GetLastWriteTime(p).Ticks));
        if(signature==lastSignature){Thumbnails.RequestVisible(scroll);return;}lastSignature=signature;tiles.Children.Clear();
        foreach(string path in items)tiles.Children.Add(MakeTile(path));
        if(items.Length==0){var text=Ui.Text(IsFolderPanel?"此文件夹为空":viewed.Kind=="工作"||viewed.Kind=="应用"?"暂时没有匹配的桌面文件":"拖入文件或快捷方式",12,Ui.DesktopMuted);text.Margin=new Thickness(12,20,12,12);tiles.Children.Add(text);}
    }
    void ClosePreview(){if(previewPath==null)return;previewPath=null;scroll.Content=tiles;lastSignature=null;Refresh();}
    void NavigateBack(){if(portal==null)return;if(String.Equals(portal.Current,portal.Root,StringComparison.OrdinalIgnoreCase)&&transientPortal){portal=null;transientPortal=false;}else portal.Parent();lastSignature=null;Refresh();}
    void SelectGroup(Group selected){if(selected==null)return;previewPath=null;scroll.Content=tiles;Group.ActiveGroupId=selected.Id;EnsureSessions();lastSignature=null;controller.Store.Save();Refresh();}
    void RefreshGroupTabs(){
        if(editingTab)return;groupTabs.Children.Clear();var members=controller.Store.Data.Groups.Where(g=>g.HostGroupId==Group.Id).ToArray();groupTabsScroll.Visibility=members.Length==0?Visibility.Collapsed:Visibility.Visible;
        if(members.Length>0&&Group.Folders.Count==0){var own=Ui.Text(Group.Name,HeaderFont,Group.ActiveGroupId==Group.Id?Ui.DesktopInk:Ui.DesktopMuted);own.FontWeight=FontWeights.SemiBold;own.Margin=new Thickness(7,2,7,2);var ownTab=new Border(){Child=own,Cursor=Cursors.Hand,BorderBrush=Ui.Brush(Group.Color),BorderThickness=new Thickness(0,0,0,Group.ActiveGroupId==Group.Id?2:0),Margin=new Thickness(0,0,4,0)};ownTab.MouseLeftButtonDown+=delegate(object s,MouseButtonEventArgs e){e.Handled=true;if(e.ClickCount==2){EditTabName(Group,ownTab);return;}SelectGroup(Group);};groupTabs.Children.Add(ownTab);}
        foreach(var member in members){Group selected=member;var label=Ui.Text(member.Name,HeaderFont,Ui.DesktopInk);label.FontWeight=FontWeights.SemiBold;label.Margin=new Thickness(7,2,2,2);var close=Ui.Text("×",12,Ui.DesktopMuted);close.Margin=new Thickness(2,2,6,2);close.Cursor=Cursors.Hand;close.Opacity=0;close.IsHitTestVisible=false;close.MouseLeftButtonDown+=delegate(object s,MouseButtonEventArgs e){e.Handled=true;controller.DetachGroup(Group,selected);};var row=new StackPanel(){Orientation=Orientation.Horizontal};row.Children.Add(label);row.Children.Add(close);var tab=new Border(){Child=row,Cursor=Cursors.Hand,Background=Brushes.Transparent,BorderBrush=Ui.Brush(Group.Color),BorderThickness=new Thickness(0,0,0,member.Id==Group.ActiveGroupId?2:0),Margin=new Thickness(0,0,4,0)};
            tab.MouseEnter+=delegate{close.Opacity=1;close.IsHitTestVisible=true;};tab.MouseLeave+=delegate{close.Opacity=0;close.IsHitTestVisible=false;};
            tab.MouseLeftButtonDown+=delegate(object s,MouseButtonEventArgs e){e.Handled=true;if(e.ClickCount==2){EditTabName(selected,tab);return;}SelectGroup(selected);};
            var menu=new ContextMenu();menu.Items.Add(Ui.Menu("切换到此分区",delegate{SelectGroup(selected);}));menu.Items.Add(Ui.Menu("重命名",delegate{EditTabName(selected,tab);}));menu.Items.Add(Ui.Menu("关闭标签并恢复独立分区",delegate{controller.DetachGroup(Group,selected);}));tab.ContextMenu=menu;groupTabs.Children.Add(tab);
        }
    }
    void EditTabName(Group target,Border tab){if(editingTab)return;editingTab=true;var label=tab.Child;var editor=new TextBox(){Text=target.Name,MinWidth=80,MaxWidth=150,FontSize=HeaderFont};tab.Child=editor;bool done=false;Action<bool> finish=delegate(bool save){if(done)return;done=true;if(save&&!String.IsNullOrWhiteSpace(editor.Text)&&editor.Text.Trim()!=target.Name){controller.Store.Checkpoint();target.Name=editor.Text.Trim();controller.Store.Save();}tab.Child=label;editingTab=false;Refresh();};editor.KeyDown+=delegate(object s,KeyEventArgs e){if(e.Key==Key.Enter){finish(true);e.Handled=true;}else if(e.Key==Key.Escape){finish(false);e.Handled=true;}};editor.LostKeyboardFocus+=delegate{finish(true);};editor.Focus();editor.SelectAll();}
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
    void RefreshWorkTabs(){workTabs.Children.Clear();foreach(string view in new[]{"文档","图片"}){string selected=view;bool active=ViewGroup.WorkView==view;var label=Ui.Text(view,HeaderFont,active?Ui.DesktopInk:Ui.DesktopMuted);label.FontWeight=FontWeights.SemiBold;label.Margin=new Thickness(8,2,8,2);var tab=new Border(){Child=label,Cursor=Cursors.Hand,Background=Brushes.Transparent,BorderBrush=Ui.Brush(Group.Color),BorderThickness=new Thickness(0,0,0,active?2:0),Margin=new Thickness(0,0,4,0)};tab.MouseLeftButtonDown+=delegate(object s,MouseButtonEventArgs e){e.Handled=true;if(ViewGroup.WorkView==selected)return;controller.Store.Checkpoint();ViewGroup.WorkView=selected;lastSignature=null;controller.Store.Save();Refresh();};workTabs.Children.Add(tab);}}
    string FolderLabel(string path){string name=Path.GetFileName((path??"").TrimEnd(Path.DirectorySeparatorChar));return String.IsNullOrEmpty(name)?(path??""):name;}
    void EnsureSessions(){
        var folders=FolderGroup;
        sessions.Clear();foreach(string folder in folders.Folders)sessions.Add(new PortalSession(folder));
        if(folders.FolderIndex>=sessions.Count)folders.FolderIndex=sessions.Count==0?0:sessions.Count-1;
        if(folders.FolderIndex<0&&folders.Kind=="文件夹"&&!String.IsNullOrEmpty(folders.Folder))folders.FolderIndex=0;
        portal=ViewGroup!=folders||folders.FolderIndex<0||sessions.Count==0?null:sessions[folders.FolderIndex];transientPortal=false;
    }
    void RefreshTabs(){
        if(tabBar==null)return;tabBar.Children.Clear();
        var folders=FolderGroup;
        if(folders.Kind!="文件夹"||String.IsNullOrEmpty(folders.Folder)){var homeLabel=Ui.Text(folders.Kind=="文件夹"?(folders.Name=="文件夹"?"桌面":folders.Name):"分区",HeaderFont,portal==null?Ui.DesktopInk:Ui.DesktopMuted);homeLabel.FontWeight=FontWeights.SemiBold;homeLabel.Margin=new Thickness(7,2,7,2);var home=new Border(){Child=homeLabel,Cursor=Cursors.Hand,BorderBrush=Ui.Brush(Group.Color),BorderThickness=new Thickness(0,0,0,portal==null?2:0),Margin=new Thickness(0,0,6,0)};home.MouseLeftButtonDown+=delegate(object s,MouseButtonEventArgs e){e.Handled=true;if(e.ClickCount==2&&ViewGroup==Group){RenameGroup(Group);return;}SelectTab(-1);};tabBar.Children.Add(home);}
        var tint=((SolidColorBrush)Ui.Brush(Group.Color)).Color;
        for(int i=0;i<folders.Folders.Count;i++){
            int index=i;bool active=ViewGroup==folders&&index==folders.FolderIndex;
            var label=Ui.Text(FolderLabel(folders.Folders[i]),HeaderFont,Ui.DesktopInk);label.FontWeight=FontWeights.SemiBold;label.VerticalAlignment=VerticalAlignment.Center;
            var close=new TextBlock(){Text="×",FontSize=12,Foreground=Ui.DesktopMuted,Margin=new Thickness(6,0,2,0),Cursor=Cursors.Hand,VerticalAlignment=VerticalAlignment.Center,Opacity=0,IsHitTestVisible=false};close.MouseLeftButtonDown+=delegate(object s,MouseButtonEventArgs e){e.Handled=true;RemoveFolder(index);};
            var row=new StackPanel(){Orientation=Orientation.Horizontal};row.Children.Add(label);row.Children.Add(close);
            var tab=new Border(){Child=row,Cursor=Cursors.Hand,Padding=new Thickness(6,2,4,2),Margin=new Thickness(0,0,6,0),Background=Brushes.Transparent,BorderBrush=Ui.Brush(Group.Color),BorderThickness=new Thickness(0,0,0,active?2:0),ToolTip=folders.Folders[i]};
            tab.MouseEnter+=delegate{close.Opacity=1;close.IsHitTestVisible=true;};tab.MouseLeave+=delegate{close.Opacity=0;close.IsHitTestVisible=false;};
            tab.MouseLeftButtonDown+=delegate(object s,MouseButtonEventArgs e){SelectTab(index);e.Handled=true;};
            var menu=new ContextMenu();menu.Items.Add(Ui.Menu("在面板中打开",delegate{SelectTab(index);}));menu.Items.Add(Ui.Menu("在资源管理器中打开",delegate{Shell.Open(folders.Folders[index]);}));menu.Items.Add(Ui.Menu("关闭标签",delegate{RemoveFolder(index);}));tab.ContextMenu=menu;
            tabBar.Children.Add(tab);
        }
        var add=new TextBlock(){Text="＋",FontSize=17,Foreground=Ui.DesktopMuted,Cursor=Cursors.Hand,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(2,0,6,0),ToolTip="添加文件夹"};add.MouseLeftButtonDown+=delegate(object s,MouseButtonEventArgs e){e.Handled=true;AddFolderDialog();};tabBar.Children.Add(add);}
    void SelectTab(int index){
        var folders=FolderGroup;
        if(index<0&&(folders.Kind!="文件夹"||String.IsNullOrEmpty(folders.Folder))){ClosePreview();folders.FolderIndex=-1;portal=null;transientPortal=false;lastSignature=null;controller.Store.Save();Refresh();return;}
        if(index<0||index>=folders.Folders.Count)return;ClosePreview();bool same=ViewGroup==folders&&index==folders.FolderIndex;if(folders==Group)Group.ActiveGroupId=Group.Id;EnsureSessions();portal=sessions[index];transientPortal=false;
        if(same&&portal!=null&&!String.Equals(portal.Current,portal.Root,StringComparison.OrdinalIgnoreCase)){portal=new PortalSession(folders.Folders[index]);sessions[index]=portal;}
        folders.FolderIndex=index;lastSignature=null;controller.Store.Save();Refresh();
    }
    void AddFolderDialog(){if(controller.Manager==null)return;using(var dialog=new System.Windows.Forms.FolderBrowserDialog()){dialog.Description="选择要在面板中显示的文件夹";if(dialog.ShowDialog()!=System.Windows.Forms.DialogResult.OK)return;AddFolders(new[]{dialog.SelectedPath});}}
    void AddFolders(string[] paths){
        if(paths==null||paths.Length==0)return;var folders=FolderGroup;var added=new List<string>();foreach(string path in paths){string full;try{full=Path.GetFullPath(path);}catch{continue;}if(!Directory.Exists(full))continue;if(folders.Folders.Any(f=>String.Equals(f,full,StringComparison.OrdinalIgnoreCase))||added.Any(f=>String.Equals(f,full,StringComparison.OrdinalIgnoreCase)))continue;added.Add(full);}
        if(added.Count==0)return;ClosePreview();controller.Store.Checkpoint();folders.Folders.AddRange(added);folders.FolderIndex=folders.Folders.Count-added.Count;if(folders==Group)Group.ActiveGroupId=Group.Id;EnsureSessions();lastSignature=null;controller.SaveAndRefresh(false);Refresh();
    }
    void RemoveFolder(int index){
        var folders=FolderGroup;if(index<0||index>=folders.Folders.Count)return;controller.Store.Checkpoint();string removed=folders.Folders[index];folders.Folders.RemoveAt(index);folders.FolderIgnoreRules.Remove(removed);if(folders.FolderIndex>=folders.Folders.Count)folders.FolderIndex=folders.Folders.Count==0?-1:folders.Folders.Count-1;if(String.Equals(folders.Folder,removed,StringComparison.OrdinalIgnoreCase))folders.Folder=folders.Folders.Count>0?folders.Folders[0]:"";EnsureSessions();lastSignature=null;controller.SaveAndRefresh(false);Refresh();
    }
    string lastSignature=null;
    Border MakeTile(string path){
        bool listMode=ViewGroup.ViewMode!="图标";var box=new Border(){Width=listMode?Math.Max(170,Width-38):Math.Max(88,ViewGroup.IconSize+36),Height=listMode?(ViewGroup.ViewMode=="详细信息"?54:38):ViewGroup.IconSize+61,Margin=new Thickness(2),CornerRadius=new CornerRadius(8),Background=Brushes.Transparent,Tag="qidesk-file",Cursor=Cursors.Hand};
        box.ToolTip=Path.GetFileName(path)+"\n"+FilePresentation.Detail(path);box.BorderThickness=new Thickness(1);box.BorderBrush=Brushes.Transparent;
        var stack=new StackPanel(){Margin=new Thickness(4,8,4,0)};double size=listMode?24:ViewGroup.IconSize;bool picture=Thumbnails.IsPicture(path),application=FilePresentation.IsApplication(path);
        Color accent=((SolidColorBrush)Ui.Brush(ViewGroup.IconColor)).Color;var themed=application?null:FluentIcons.Icon(path,ViewGroup.IconSkin,accent);
        var icon=new Image(){Source=themed??Shell.Icon(path),Width=picture?size:size*.84,Height=picture?size:size*.84,Stretch=Stretch.Uniform,SnapsToDevicePixels=true};
        RenderOptions.SetBitmapScalingMode(icon,BitmapScalingMode.HighQuality);Thumbnails.Bind(icon,path,size);
        double plateSize=size+(listMode?6:10);string skin=ViewGroup.IconSkin;
        Brush plateBackground=skin=="Fluent 线条"?new SolidColorBrush(Color.FromArgb(225,236,243,250)):skin=="Fluent 填充"?new SolidColorBrush(Color.FromArgb(240,28,40,60)):picture?(Brush)Brushes.White:new LinearGradientBrush(Color.FromArgb(235,36,54,78),Color.FromArgb(245,19,32,50),90);
        var plate=new Border(){Width=plateSize,Height=plateSize,CornerRadius=new CornerRadius(skin=="Fluent 填充"?plateSize/2:skin=="Fluent 线条"?Math.Max(9,plateSize*.25):picture?Math.Max(5,plateSize*.18):Math.Max(7,plateSize*.27)),BorderThickness=new Thickness(skin=="Fluent 线条"?1.5:1),BorderBrush=skin=="经典"?(picture?Ui.Brush("#D6E2EE"):application?Ui.Brush(ViewGroup.Color):Ui.Brush("#8296AD")):new SolidColorBrush(accent),Background=plateBackground,Child=icon,Margin=new Thickness(0,0,listMode?8:0,listMode?0:7),Effect=Ui.Shadow(1,5,.18)};
        if(picture)icon.Clip=new RectangleGeometry(new Rect(0,0,size,size),Math.Max(4,size*.16),Math.Max(4,size*.16));
        FrameworkElement visual=plate;
        var name=new TextBlock(){Text=FilePresentation.DisplayName(path,controller.Store.Data.ShowFileExtensions),Foreground=Ui.DesktopInk,FontSize=application?11.5:11,TextAlignment=listMode?TextAlignment.Left:TextAlignment.Center,TextWrapping=listMode?TextWrapping.NoWrap:TextWrapping.Wrap,MaxHeight=34,TextTrimming=TextTrimming.CharacterEllipsis,Effect=Ui.Shadow(1,3,.8)};
        if(listMode){var row=new DockPanel(){Margin=new Thickness(7,4,7,4)};DockPanel.SetDock(visual,Dock.Left);row.Children.Add(visual);var detail=new StackPanel(){VerticalAlignment=VerticalAlignment.Center};detail.Children.Add(name);if(ViewGroup.ViewMode=="详细信息")detail.Children.Add(new TextBlock(){Text=FilePresentation.Detail(path),FontSize=10,Foreground=Ui.DesktopMuted,TextTrimming=TextTrimming.CharacterEllipsis});row.Children.Add(detail);box.Child=row;}else{stack.Children.Add(visual);stack.Children.Add(name);box.Child=stack;}
        Point press=new Point();bool held=false;
        box.MouseEnter+=delegate{box.Background=new SolidColorBrush(Color.FromArgb(70,110,142,184));box.BorderBrush=new SolidColorBrush(Color.FromArgb(110,232,241,255));};
        box.MouseLeave+=delegate{box.Background=Brushes.Transparent;box.BorderBrush=Brushes.Transparent;};
        box.MouseLeftButtonDown+=delegate(object s,MouseButtonEventArgs e){if(e.ClickCount==2){held=false;box.ReleaseMouseCapture();if(Directory.Exists(path)){if(portal==null){portal=new PortalSession(path);transientPortal=true;}else portal.Navigate(path);lastSignature=null;Refresh();}else if(!ShowPreview(path))Shell.Open(path);e.Handled=true;return;}press=e.GetPosition(box);held=true;box.CaptureMouse();e.Handled=true;};
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
        if(selectGlyph!=null){selectGlyph.Text=selected?"●":"○";selectGlyph.Foreground=selected?Ui.Accent:Ui.DesktopMuted;}
    }
}
}
