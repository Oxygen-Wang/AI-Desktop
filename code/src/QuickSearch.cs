using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace QiDesk {
// 搜索栏：默认只有一条极简输入框；按 Enter 才查询；结果面板沿用桌面分区的图标网格、
// 悬停/选中效果、右键菜单和键盘操作，只是整体用圆角面板呈现。
public sealed class QuickSearchView : UserControl,IDisposable {
    readonly Controller controller;
    readonly Store store;
    readonly TextBlock status;
    readonly TextBlock placeholder;
    readonly WrapPanel tiles;
    readonly ScrollViewer scroll;
    readonly List<SearchResult> items=new List<SearchResult>();
    CancellationTokenSource cancellation;
    bool disposed;
    Window preview;
    int generation;
    string selectedPath;
    string lastQuery;
    double iconSize=56;
    public readonly TextBox Input;
    public event Action CloseRequested;
    public string Provider {get;private set;}
    public string StatusText {get{return status.Text;}}
    public int ResultCount {get{return items.Count;}}
    public string[] ResultPaths {get{var paths=new string[items.Count];for(int i=0;i<items.Count;i++)paths[i]=items[i].Path;return paths;}}
    public bool PreviewVisible {get{return preview!=null&&preview.IsVisible;}}
    public bool MenuOpen {get;private set;}
    public event Action ResultsChanged;
    public QuickSearchView(Controller owner,bool desktop=false){
        controller=owner;store=owner.Store;Foreground=desktop?Ui.DesktopInk:Ui.Ink;
        var grid=new Grid(){Margin=new Thickness(14)};
        grid.RowDefinitions.Add(new RowDefinition(){Height=GridLength.Auto});
        grid.RowDefinitions.Add(new RowDefinition(){Height=GridLength.Auto});
        grid.RowDefinitions.Add(new RowDefinition());
        Content=grid;
        var row=new DockPanel();grid.Children.Add(row);
        var glyph=Ui.Text("⌕",28,Foreground);glyph.Margin=new Thickness(0,0,10,0);glyph.VerticalAlignment=VerticalAlignment.Center;DockPanel.SetDock(glyph,Dock.Left);row.Children.Add(glyph);
        var close=Ui.Button("×",RequestClose);close.ToolTip=desktop?"清空搜索":"收起搜索 · Esc";close.FontSize=20;close.Width=36;close.Height=36;close.Padding=new Thickness(0);close.Margin=new Thickness(8,0,0,0);DockPanel.SetDock(close,Dock.Right);row.Children.Add(close);
        Input=Ui.Input("");Input.FontSize=20;Input.MinHeight=42;Input.Margin=new Thickness(0);Input.Foreground=Foreground;
        Input.Background=Brushes.Transparent;Input.BorderThickness=new Thickness(0);Input.Padding=new Thickness(0,2,0,2);Input.ToolTip="输入后按 Enter 搜索全部文件 · 支持 ext:pdf、folder:、路径等 Everything 表达式";
        placeholder=Ui.Text("搜索文件、文档、图片…",19,desktop?Ui.DesktopMuted:Ui.Muted);
        placeholder.Margin=new Thickness(2,0,0,0);placeholder.VerticalAlignment=VerticalAlignment.Center;placeholder.IsHitTestVisible=false;
        var inputHost=new Grid();inputHost.Children.Add(Input);inputHost.Children.Add(placeholder);
        row.Children.Add(new Border(){Child=inputHost,Background=desktop?Ui.Brush("#263449"):Ui.Brush("#EDF3F8"),CornerRadius=new CornerRadius(18),Padding=new Thickness(12,0,12,0)});
        status=Ui.Text("",11,desktop?Ui.DesktopMuted:Ui.Muted);status.Margin=new Thickness(0,8,0,8);status.Visibility=Visibility.Collapsed;Grid.SetRow(status,1);grid.Children.Add(status);
        tiles=new WrapPanel(){Orientation=Orientation.Horizontal,Margin=new Thickness(0,2,0,2)};
        scroll=new ScrollViewer(){Content=tiles,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Focusable=false};
        Grid.SetRow(scroll,2);grid.Children.Add(scroll);
        Input.TextChanged+=delegate{
            Cancel();ClearTiles();lastQuery=null;
            placeholder.Visibility=Input.Text.Length==0?Visibility.Visible:Visibility.Collapsed;
            if(Input.Text.Trim().Length==0){status.Text="";status.Visibility=Visibility.Collapsed;}
            else{status.Visibility=Visibility.Visible;status.Text="按 Enter 搜索";}
            NotifyResults();
        };
        PreviewKeyDown+=delegate(object s,KeyEventArgs e){
            if(e.Key==Key.LeftCtrl||e.Key==Key.RightCtrl){ShowPreview();return;}
            if(e.Key==Key.Escape){RequestClose();e.Handled=true;}
            else if(e.Key==Key.Enter){if(ResultCount==0)Search();else OpenSelected((Keyboard.Modifiers&ModifierKeys.Control)!=0);e.Handled=true;}
            else if(e.Key==Key.F5){Search();e.Handled=true;}
            else if(e.Key==Key.Left){MoveSelection(-1);e.Handled=true;}
            else if(e.Key==Key.Right){MoveSelection(1);e.Handled=true;}
            else if(e.Key==Key.Up){MoveRow(-1);e.Handled=true;}
            else if(e.Key==Key.Down){MoveRow(1);e.Handled=true;}
        };
        PreviewKeyUp+=delegate(object s,KeyEventArgs e){if(e.Key==Key.LeftCtrl||e.Key==Key.RightCtrl)HidePreview();};
        PreviewMouseWheel+=delegate(object s,MouseWheelEventArgs e){
            if((Keyboard.Modifiers&ModifierKeys.Control)==0)return;
            e.Handled=true;
            iconSize=Math.Max(36,Math.Min(96,iconSize+(e.Delta>0?6:-6)));
            RebuildTiles();
        };
        IsVisibleChanged+=delegate{if(!IsVisible){Cancel();HidePreview();}};
    }
    public void FocusInput(){Input.Focus();Keyboard.Focus(Input);Input.SelectAll();}
    public void Reset(){Cancel();Input.Clear();ClearTiles();}
    void NotifyResults(){if(ResultsChanged!=null)ResultsChanged();}
    void RequestClose(){if(CloseRequested!=null)CloseRequested();else Reset();}
    void Cancel(){generation++;HidePreview();if(cancellation!=null){cancellation.Cancel();cancellation.Dispose();cancellation=null;}}
    Group SearchStyle(){var group=store.Data.Groups.Count>0?store.Data.Groups[0]:null;return group??new Group();}
    double TileWidth {get{return iconSize+46;}}
    void ClearTiles(){items.Clear();tiles.Children.Clear();selectedPath=null;}
    int SelectedIndex(){return items.FindIndex(r=>String.Equals(r.Path,selectedPath,StringComparison.OrdinalIgnoreCase));}
    void Select(int index){
        if(items.Count==0)return;
        index=Math.Max(0,Math.Min(items.Count-1,index));
        for(int i=0;i<tiles.Children.Count;i++){
            var tile=tiles.Children[i] as Border;
            if(tile==null)continue;
            bool active=i==index;
            tile.BorderBrush=active?Ui.Accent:Brushes.Transparent;
            tile.Background=active?new SolidColorBrush(Color.FromArgb(38,0,106,194)):Brushes.Transparent;
        }
        selectedPath=items[index].Path;
        var element=tiles.Children[index] as FrameworkElement;
        if(element!=null)element.BringIntoView();
    }
    void MoveSelection(int delta){
        if(items.Count==0)return;int index=SelectedIndex();if(index<0)index=0;Select(index+delta);
    }
    void MoveRow(int direction){
        if(items.Count==0)return;
        int columns=Math.Max(1,(int)(scroll.ViewportWidth/Math.Max(1,TileWidth)));
        int index=SelectedIndex();if(index<0)index=0;
        Select(index+direction*columns);
    }
    void RebuildTiles(){
        string keep=selectedPath;tiles.Children.Clear();
        foreach(var result in items)tiles.Children.Add(MakeTile(result));
        int index=String.IsNullOrEmpty(keep)?0:Math.Max(0,items.FindIndex(r=>String.Equals(r.Path,keep,StringComparison.OrdinalIgnoreCase)));
        if(items.Count>0)Select(index);
    }
    // 结果图标沿用桌面分区的底板、缩略图和悬停样式。
    Border MakeTile(SearchResult result){
        var stack=new StackPanel(){Margin=new Thickness(2,6,2,0)};
        stack.Children.Add(FileTiles.Plate(result.Path,SearchStyle(),iconSize,false,store.Data));
        var label=new TextBlock(){Text=FilePresentation.DisplayName(result.Path,store.Data.ShowFileExtensions),Foreground=Foreground,FontSize=11,TextAlignment=TextAlignment.Center,TextWrapping=TextWrapping.Wrap,MaxHeight=34,TextTrimming=TextTrimming.CharacterEllipsis};
        stack.Children.Add(label);
        var box=new Border(){Width=TileWidth,Height=iconSize+66,Margin=new Thickness(3),CornerRadius=new CornerRadius(10),Background=Brushes.Transparent,BorderThickness=new Thickness(1),BorderBrush=Brushes.Transparent,Tag=result,Cursor=Cursors.Hand};
        box.ToolTip=result.Path+"\n"+FilePresentation.Detail(result.Path);box.Child=stack;
        box.MouseEnter+=delegate{if(!String.Equals(selectedPath,result.Path,StringComparison.OrdinalIgnoreCase))box.Background=new SolidColorBrush(Color.FromArgb(70,110,142,184));};
        box.MouseLeave+=delegate{if(!String.Equals(selectedPath,result.Path,StringComparison.OrdinalIgnoreCase))box.Background=Brushes.Transparent;};
        box.MouseLeftButtonDown+=delegate(object s,MouseButtonEventArgs e){
            Select(items.FindIndex(r=>Object.ReferenceEquals(r,result)));
            if(e.ClickCount==2){OpenResult(result,false);e.Handled=true;}
        };
        box.ContextMenu=new ContextMenu();
        box.ContextMenuOpening+=delegate(object s,ContextMenuEventArgs e){
            e.Handled=true;Select(items.FindIndex(r=>Object.ReferenceEquals(r,result)));
            if(controller.Manager==null)return;
            var host=Window.GetWindow(this);if(host==null)return;
            Point point=e.CursorLeft<0?box.PointToScreen(new Point(12,12)):PointToScreen(Mouse.GetPosition(this));
            MenuOpen=true;
            try{controller.Manager.ShowFileMenu(host,result.Path,null,point,false,RefreshExisting);}
            finally{
                host.Dispatcher.BeginInvoke(new Action(delegate{
                    MenuOpen=false;
                    if(!disposed&&!host.IsActive)host.Hide();
                }),DispatcherPriority.ApplicationIdle);
            }
        };
        return box;
    }
    SearchResult SelectedResult(){int index=SelectedIndex();return index<0?null:items[index];}
    void ShowPreview(){
        var result=SelectedResult();
        if(result==null||!Store.Exists(result.Path))return;
        HidePreview();
        var content=new StackPanel(){Margin=new Thickness(20)};
        content.Children.Add(Ui.Text(result.Name,19,Ui.Ink));
        content.Children.Add(Ui.Text(result.Path,12,Ui.Muted));
        if(Thumbnails.IsPicture(result.Path)){
            var picture=new ThumbnailImage(){Width=460,Height=300,Margin=new Thickness(0,16,0,12),FilePath=result.Path};
            content.Children.Add(picture);
        }
        content.Children.Add(Ui.Text(FilePresentation.Detail(result.Path),12,Ui.Muted));
        preview=new Window(){Title="搜索预览",Width=520,Height=Thumbnails.IsPicture(result.Path)?400:160,
            WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,AllowsTransparency=true,
            Background=Brushes.Transparent,ShowInTaskbar=false,ShowActivated=false,Topmost=true,
            Content=new Border(){Child=content,Background=Ui.Base,CornerRadius=new CornerRadius(18),BorderBrush=Ui.Brush("#B6CBDC"),BorderThickness=new Thickness(1)}};
        var host=Window.GetWindow(this);
        if(host!=null){preview.Left=Math.Min(SystemParameters.WorkArea.Right-preview.Width,host.Left+host.Width+12);preview.Top=Math.Max(SystemParameters.WorkArea.Top,Math.Min(host.Top,SystemParameters.WorkArea.Bottom-preview.Height));}
        preview.Show();
    }
    void HidePreview(){if(preview==null)return;var current=preview;preview=null;current.Close();}
    // 查询在后台线程跑；结果和错误一律回到控件自己的 Dispatcher，测试宿主没有
    // WPF 同步上下文时也不会跨线程改 TextBlock。
    async void Search(){
        string query=Input.Text.Trim();
        if(disposed||query.Length==0)return;
        Cancel();int version=generation;lastQuery=query;
        status.Visibility=Visibility.Visible;status.Text="正在搜索…";
        var state=Store.Json.Deserialize<State>(Store.Json.Serialize(store.Data));
        cancellation=new CancellationTokenSource();var token=cancellation.Token;
        try{
            var response=await Task.Run(()=>FileSearch.Query(new Store("",false){Data=state},query,false,200,null,false,token),token);
            var posted=Dispatcher.BeginInvoke(new Action(delegate{
                if(disposed||token.IsCancellationRequested||version!=generation)return;
                Present(response);
            }));
        }catch(OperationCanceledException){}
        catch(Exception ex){
            string message="搜索失败："+ex.Message;
            var postedError=Dispatcher.BeginInvoke(new Action(delegate{if(!disposed&&version==generation)status.Text=message;}));
        }
    }
    internal void Present(SearchResponse response){
        Provider=response.Provider;status.Visibility=Visibility.Visible;status.Text=response.Provider+" · "+response.Items.Length+" 项 · "+response.Message;
        ClearTiles();
        foreach(var result in response.Items)items.Add(result);
        RebuildTiles();
        if(items.Count==0){var hint=Ui.Text("没有找到匹配的文件",12,Foreground);hint.Margin=new Thickness(6,18,6,6);tiles.Children.Add(hint);}
        NotifyResults();
    }
    // 右键菜单执行重命名/删除后，只保留仍存在的文件，避免点开失效项。
    void RefreshExisting(){
        bool removed=false;
        for(int i=items.Count-1;i>=0;i--)if(!Store.Exists(items[i].Path)){items.RemoveAt(i);removed=true;}
        if(removed){RebuildTiles();status.Visibility=Visibility.Visible;status.Text=(String.IsNullOrEmpty(Provider)?"搜索":Provider)+" · "+items.Count+" 项";}
        NotifyResults();
    }
    void OpenSelected(bool reveal){var result=SelectedResult();if(result!=null)OpenResult(result,reveal);}
    void OpenResult(SearchResult result,bool reveal){
        if(!Store.Exists(result.Path)){status.Text="项目已移动、删除或当前不可用，请重新搜索。";return;}
        try{if(reveal)Shell.Reveal(result.Path);else Shell.Open(result.Path);if(CloseRequested!=null)CloseRequested();}
        catch(System.ComponentModel.Win32Exception ex){status.Text="无法打开："+ex.Message;}
    }
    public void Dispose(){if(disposed)return;disposed=true;Cancel();}
}
public sealed class QuickSearchWindow : Window {
    public readonly QuickSearchView Search;
    bool userSized;
    public QuickSearchWindow(Controller controller){
        Title="栖桌 · 搜索全部";Width=660;Height=420;MinWidth=380;WindowStyle=WindowStyle.None;
        ResizeMode=ResizeMode.NoResize;AllowsTransparency=true;Background=Brushes.Transparent;
        ShowInTaskbar=false;Topmost=true;FontFamily=new FontFamily("Microsoft YaHei UI");
        Search=new QuickSearchView(controller);
        var frame=new Border(){Child=Search,Background=Ui.Base,CornerRadius=new CornerRadius(18),BorderBrush=Ui.Brush("#B6CBDC"),BorderThickness=new Thickness(1)};
        // 和桌面分区一样可以从右下角拖动改变面板大小；结果区跟着变高变宽。
        var gripHost=new Grid(){Width=22,Height=22,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,Background=Brushes.Transparent,ToolTip="拖动调整搜索结果面板大小"};
        gripHost.Children.Add(new TextBlock(){Text="◢",FontSize=13,Foreground=Ui.Muted,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(0,0,3,1),IsHitTestVisible=false});
        var grip=new Thumb(){Width=22,Height=22,Cursor=Cursors.SizeNWSE,Background=Brushes.Transparent,Tag="qidesk-search-resize"};
        grip.DragDelta+=delegate(object s,System.Windows.Controls.Primitives.DragDeltaEventArgs e){
            userSized=true;
            var area=SystemParameters.WorkArea;
            Width=Math.Max(380,Math.Min(area.Width-24,Width+e.HorizontalChange));
            Height=Math.Max(140,Math.Min(area.Height-40,Height+e.VerticalChange));
        };
        gripHost.Children.Add(grip);
        var root=new Grid();root.Children.Add(frame);root.Children.Add(gripHost);Content=root;
        Search.ResultsChanged+=FitResults;
        Search.CloseRequested+=Hide;Deactivated+=delegate{Dispatcher.BeginInvoke(new Action(delegate{if(!IsActive&&!Search.MenuOpen)Hide();}));};
        Closed+=delegate{Search.Dispose();};
    }
    public void Present(){
        var area=SystemParameters.WorkArea;
        if(!userSized)Width=Math.Min(660,area.Width-32);
        FitResults();
        Left=area.Left+(area.Width-Width)/2;Top=area.Top+Math.Max(24,(area.Height-Height)*.28);
        Show();Activate();Search.FocusInput();
    }
    // 空闲时只有一条输入框；输入后提示按 Enter；结果出现在同一块圆角分区式面板里。
    // 用户手动拖过大小后保留自定义尺寸，清空搜索时才回到小栏位。
    void FitResults(){
        if(Search.ResultCount>0){if(!userSized)Height=Math.Min(560,SystemParameters.WorkArea.Height-48);return;}
        userSized=false;Height=Search.Input.Text.Trim().Length==0?76:102;
    }
}
public partial class ManagerWindow {
    QuickSearchWindow quickSearch;
    public bool QuickSearchHotkeyRegistered {get;private set;}
    public bool QuickSearchVisible {get{return quickSearch!=null&&quickSearch.IsVisible;}}
    public void ShowQuickSearch(bool toggle=false){
        if(quickSearch!=null&&quickSearch.IsVisible&&toggle){quickSearch.Hide();return;}
        if(quickSearch==null)quickSearch=new QuickSearchWindow(controller);
        quickSearch.Present();
    }
    public void UpdateQuickSearchHotkey(){
        QuickSearchHotkeyRegistered=false;if(hwnd==null)return;
        Shell.UnregisterHotKey(hwnd.Handle,8);
        if(!controller.Store.Data.QuickSearchAll)return;
        QuickSearchHotkeyRegistered=Shell.RegisterHotKey(hwnd.Handle,8,3|0x4000,0x20);
        if(!QuickSearchHotkeyRegistered)SetStatus("Ctrl＋Alt＋Space 已被占用，可从文件搜索页或托盘打开搜索。");
    }
    void CloseQuickSearch(){if(hwnd!=null)Shell.UnregisterHotKey(hwnd.Handle,8);if(quickSearch!=null){quickSearch.Close();quickSearch=null;}}
}
public partial class WidgetWindow {
    QuickSearchView quickSearchView;
    void BuildQuickSearch(){
        quickSearchView=new QuickSearchView(controller,true);quickSearchView.Margin=new Thickness(-8,0,-8,0);
        body.Children.Add(quickSearchView);
        Action fit=delegate{quickSearchView.Height=Math.Max(90,Height-64);};fit();SizeChanged+=delegate{fit();};
        quickSearchView.Input.PreviewMouseLeftButtonDown+=delegate{PrepareInlineInput(quickSearchView.Input);};
        Closed+=delegate{quickSearchView.Dispose();};
    }
}
}
