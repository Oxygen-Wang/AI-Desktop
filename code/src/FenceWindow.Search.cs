using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace QiDesk {
public partial class FenceWindow {
    Border searchBar;
    TextBox searchInput;
    DispatcherTimer searchDelay,hoverDelay;
    CancellationTokenSource searchCancellation;
    string searchKey="";
    string[] searchResults=new string[0];
    bool windowClosed;
    public bool SearchVisible{get{return searchBar!=null&&searchBar.Visibility==Visibility.Visible;}}
    public TextBox SearchInput{get{return searchInput;}}
    internal string[] SearchResultPaths{get{return searchResults;}}

    void InitializeSearch(Grid grid){
        searchInput=Ui.Input("");searchInput.Margin=new Thickness(0);searchInput.ToolTip="搜索此分区的文件，Ctrl＋F 再按一次关闭";
        var row=new DockPanel();
        var close=Ui.Button("关闭搜索",ToggleSearchBar);DockPanel.SetDock(close,Dock.Right);row.Children.Add(close);row.Children.Add(searchInput);
        searchBar=new Border(){Child=row,Margin=new Thickness(12,2,10,8),Visibility=Visibility.Collapsed};
        Grid.SetRow(searchBar,1);grid.Children.Add(searchBar);
        searchDelay=new DispatcherTimer(){Interval=TimeSpan.FromMilliseconds(220)};
        searchDelay.Tick+=delegate{searchDelay.Stop();RunPartitionSearch();};
        searchInput.TextChanged+=delegate{searchKey="";lastSignature=null;Refresh();};
        PreviewKeyDown+=delegate(object sender,KeyEventArgs e){
            if((Keyboard.Modifiers&ModifierKeys.Control)!=0&&e.Key==Key.F){ToggleSearchBar();e.Handled=true;}
            else if(e.Key==Key.Escape&&SearchVisible){ToggleSearchBar();e.Handled=true;}
            else if(e.Key==Key.F5&&SearchVisible){searchKey="";Refresh();}
        };
        hoverDelay=new DispatcherTimer(){Interval=TimeSpan.FromMilliseconds(650)};
        hoverDelay.Tick+=delegate{hoverDelay.Stop();if(!windowClosed&&IsMouseOver&&!moving&&Mouse.LeftButton!=MouseButtonState.Pressed&&Group.Rolled){hoverExpanded=true;ApplyBounds();}};
        Closed+=delegate{windowClosed=true;searchDelay.Stop();hoverDelay.Stop();CancelSearch();};
    }

    void ScheduleHoverExpansion(){if(hoverDelay!=null&&Group.Rolled&&controller.Store.Data.RollupEnabled&&controller.Store.Data.HoverExpand)hoverDelay.Start();}
    void StopHoverExpansion(){if(hoverDelay!=null)hoverDelay.Stop();}
    void CancelSearch(){if(searchCancellation!=null){searchCancellation.Cancel();searchCancellation.Dispose();searchCancellation=null;}}

    public void ToggleSearchBar(){
        if(SearchVisible){
            searchBar.Visibility=Visibility.Collapsed;searchInput.Text="";searchDelay.Stop();CancelSearch();searchKey="";searchResults=new string[0];
            if(Docked&&!Topmost)Shell.EndInlineInput(this);
        }else{
            if(Group.Rolled){Group.Rolled=false;hoverExpanded=false;ApplyBounds();controller.Store.Save();}
            searchBar.Visibility=Visibility.Visible;
            Shell.BeginInlineInput(this);searchInput.Focus();Keyboard.Focus(searchInput);
        }
        lastSignature=null;Refresh();
    }

    string[] SearchItems(Group viewed){
        string query=SearchVisible?searchInput.Text.Trim():"";
        if(query.Length==0)return PartitionTabs.Items(controller.Store,viewed,portal==null?viewed.Folder:portal.Current,controller.Search).Take(1000).ToArray();
        string key=viewed.Id+"|"+(portal==null?viewed.Folder:portal.Current)+"|"+query;
        if(key!=searchKey){CancelSearch();searchKey=key;searchResults=new string[0];searchDelay.Stop();searchDelay.Start();}
        return searchResults.Where(p=>Store.Exists(p)&&!controller.Store.Ignored(p,viewed)).ToArray();
    }

    async void RunPartitionSearch(){
        if(windowClosed||!SearchVisible)return;
        string query=searchInput.Text.Trim(),key=searchKey,root=portal==null?ViewGroup.Folder:portal.Current;
        if(query.Length==0)return;
        var snapshot=Store.Json.Deserialize<State>(Store.Json.Serialize(controller.Store.Data));
        var viewed=snapshot.Groups.FirstOrDefault(g=>g.Id==ViewGroup.Id);
        CancelSearch();searchCancellation=new CancellationTokenSource();var token=searchCancellation.Token;
        try{
            string[] result=await Task.Run(delegate{
                if(!String.IsNullOrEmpty(root))return PanelFiles.Search(snapshot,root,query,null,viewed,token);
                var local=new Store("",false){Data=snapshot};
                return local.ItemsAt(viewed,query,root).Where(p=>!token.IsCancellationRequested).Take(2000).ToArray();
            },token);
            if(windowClosed||token.IsCancellationRequested||searchKey!=key)return;
            searchResults=result;lastSignature=null;Refresh();
        }catch(OperationCanceledException){}
        catch(Exception ex){if(!windowClosed&&searchKey==key&&controller.Manager!=null)controller.Manager.SetStatus("分区搜索失败："+ex.Message);}
    }
}
}
