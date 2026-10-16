using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace QiDesk {
public sealed class ActivityEntry {public string Path {get;set;}public DateTime Opened {get;set;}}
public static class FileActivity {
    static readonly object gate=new object();
    static string root;static List<ActivityEntry> entries=new List<ActivityEntry>();
    public static void Initialize(string directory){lock(gate){root=directory;try{entries=Store.Json.Deserialize<List<ActivityEntry>>(File.ReadAllText(System.IO.Path.Combine(root,"recent.json")))??new List<ActivityEntry>();}catch{entries=new List<ActivityEntry>();}}}
    public static void Record(string path){lock(gate){if(root==null||!Store.Exists(path))return;try{entries.RemoveAll(e=>String.Equals(e.Path,path,StringComparison.OrdinalIgnoreCase));entries.Insert(0,new ActivityEntry(){Path=path,Opened=DateTime.Now});entries=entries.Take(100).ToList();File.WriteAllText(System.IO.Path.Combine(root,"recent.json"),Store.Json.Serialize(entries));}catch{}}}
    public static ActivityEntry[] Recent(Store store){ActivityEntry[] snapshot;lock(gate){snapshot=entries.ToArray();}var result=snapshot.Where(e=>Store.Exists(e.Path)).ToList();try{string dir=Environment.GetFolderPath(Environment.SpecialFolder.Recent);foreach(var f in Directory.GetFiles(dir,"*.lnk").OrderByDescending(File.GetLastWriteTime).Take(100)){string p=NativeFileOperations.ResolveShortcut(f);if(Store.Exists(p))result.Add(new ActivityEntry(){Path=p,Opened=File.GetLastWriteTime(f)});}}catch{}return result.OrderByDescending(e=>e.Opened).GroupBy(e=>e.Path,StringComparer.OrdinalIgnoreCase).Select(g=>g.First()).Where(e=>!store.Ignored(e.Path,null)).Take(100).ToArray();}
}
public sealed class SearchResult {public string Path {get;set;}public long Bytes {get;set;}public DateTime Changed {get;set;}public string Name {get{return System.IO.Path.GetFileName(Path.TrimEnd('\\'));}}public string Location {get{return FilePresentation.FolderName(System.IO.Path.GetDirectoryName(Path.TrimEnd('\\')));}}}
public sealed class SearchResponse {public string Provider {get;set;}public string Message {get;set;}public SearchResult[] Items {get;set;}}
public static class FileSearch {
    public static string Executable(State s){if(File.Exists(s.EverythingPath))return s.EverythingPath;string known=System.IO.Path.GetFullPath(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","vendor","Everything-1.4.1.1032.x64","everything.exe"));if(File.Exists(known))return known;foreach(string folder in new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)}){string p=System.IO.Path.Combine(folder,"Everything","Everything.exe");if(File.Exists(p))return p;}return "";}
    // ES 命令行（es.exe）可能随项目 tools、也可能与 Everything 同目录或装在 Program Files。
    public static string EsExecutable(State s){
        var candidates=new List<string>();
        candidates.Add(System.IO.Path.GetFullPath(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","tools","everything-es","es.exe")));
        if(!String.IsNullOrEmpty(s.EverythingPath)){try{candidates.Add(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(s.EverythingPath),"es.exe"));}catch{}}
        foreach(string folder in new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)})candidates.Add(System.IO.Path.Combine(folder,"Everything","es.exe"));
        foreach(string path in candidates)if(File.Exists(path))return path;
        return candidates[0];
    }
    public static string Quote(string value){var b=new StringBuilder("\"");int slashes=0;foreach(char c in value??""){if(c=='\\'){slashes++;continue;}if(c=='\"'){b.Append('\\',slashes*2+1);b.Append(c);}else{b.Append('\\',slashes);b.Append(c);}slashes=0;}b.Append('\\',slashes*2);return b.Append('"').ToString();}
    public static void Show(State s,string query){string exe=Executable(s);if(exe.Length==0)throw new FileNotFoundException("请选择 Everything 程序。");Process.Start(new ProcessStartInfo(exe,"-search "+Quote(query)){UseShellExecute=true});}
    public static void Start(State s){string exe=Executable(s);if(exe.Length==0)throw new FileNotFoundException("未找到 Everything。");Process.Start(new ProcessStartInfo(exe,"-startup"){UseShellExecute=true,WindowStyle=ProcessWindowStyle.Hidden});}
    public static List<string[]> Csv(string text){var rows=new List<string[]>();var row=new List<string>();var cell=new StringBuilder();bool quote=false;for(int i=0;i<text.Length;i++){char c=text[i];if(c=='"'){if(quote&&i+1<text.Length&&text[i+1]=='"'){cell.Append('"');i++;}else quote=!quote;}else if(c==','&&!quote){row.Add(cell.ToString());cell.Clear();}else if((c=='\r'||c=='\n')&&!quote){if(c=='\r'&&i+1<text.Length&&text[i+1]=='\n')i++;row.Add(cell.ToString());cell.Clear();if(row.Any(x=>x.Length>0))rows.Add(row.ToArray());row.Clear();}else cell.Append(c);}if(cell.Length>0||row.Count>0){row.Add(cell.ToString());rows.Add(row.ToArray());}return rows;}
    public static string ScopedQuery(string query,string workspace){return workspace==null?(query??""):"\""+IgnoreRule.Canonical(workspace)+"\\\""+(String.IsNullOrWhiteSpace(query)?"":" <"+query+">");}
    public static string EscapeSearchOptions(string query){
        var output=new StringBuilder();bool quoted=false,start=true;
        for(int i=0;i<query.Length;i++){char ch=query[i];if(ch=='"'){quoted=!quoted;output.Append(ch);start=false;continue;}
            if(!quoted&&start&&(ch=='-'||ch=='/')){int end=i+1;while(end<query.Length&&!Char.IsWhiteSpace(query[end])&&"<>|\"".IndexOf(query[end])<0)end++;output.Append('"').Append(query.Substring(i,end-i)).Append('"');i=end-1;start=false;continue;}
            output.Append(ch);start=!quoted&&(Char.IsWhiteSpace(ch)||"<>|".IndexOf(ch)>=0);
        }return output.ToString();
    }
    public static bool InScope(string path,string workspace){return workspace==null||WorkDetection.Under(path,workspace);}
    public static bool LocalMatch(string path,string query){
        string name=System.IO.Path.GetFileName(path);foreach(string term in (query??"").Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries)){
            if(term.StartsWith("ext:",StringComparison.OrdinalIgnoreCase)){if(!term.Substring(4).Split(';').Any(ext=>System.IO.Path.GetExtension(path).Equals("."+ext,StringComparison.OrdinalIgnoreCase)))return false;}
            else if(term.Equals("folder:",StringComparison.OrdinalIgnoreCase)){if(!Directory.Exists(path))return false;}
            else if(term.Equals("file:",StringComparison.OrdinalIgnoreCase)){if(!File.Exists(path))return false;}
            else if(term.Equals("dm:today",StringComparison.OrdinalIgnoreCase)){if(File.GetLastWriteTime(path).Date!=DateTime.Today)return false;}
            else if(name.IndexOf(term,StringComparison.OrdinalIgnoreCase)<0)return false;
        }return true;
    }
    public static SearchResponse Query(Store store,string query,bool modified=false,int limit=100,string workspace=null){
        limit=Math.Max(1,Math.Min(300,limit));
        if(workspace!=null&&!WorkspaceSelection.Available(workspace))return new SearchResponse(){Provider="本机范围",Message="项目目录不可用",Items=new SearchResult[0]};
        string cli=EsExecutable(store.Data);string error="";
        if(store.Data.UseEverything&&File.Exists(cli))try{
            string expression=ScopedQuery(EscapeSearchOptions((modified?"file: ":"")+(query??"")),workspace);
            // ES uses positional search text; UTF-8 export preserves Chinese names independently of console code pages.
            string export=System.IO.Path.Combine(System.IO.Path.GetTempPath(),"QiDesk-search-"+Guid.NewGuid().ToString("N")+".csv");
            try{
            var info=new ProcessStartInfo(cli,"-n 300 -export-csv "+Quote(export)+" -utf8-bom -full-path-and-name -size -size-format 1 -no-digit-grouping -dm -date-format 1 -sort "+(modified?"date-modified-descending":"name-ascending")+" "+expression){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
            using(var p=Process.Start(info)){
                var output=p.StandardOutput.ReadToEndAsync();var stderr=p.StandardError.ReadToEndAsync();
                if(!p.WaitForExit(5000)){try{p.Kill();}catch{}throw new Exception("Everything 查询超时。");}
                if(p.ExitCode!=0)throw new Exception("Everything 未连接，请在文件搜索设置中启动。");
                output.GetAwaiter().GetResult();stderr.GetAwaiter().GetResult();string text=File.ReadAllText(export,Encoding.UTF8);var found=new List<SearchResult>();
                foreach(var row in Csv(text).Skip(1)){long size;DateTime changed;if(row.Length<3||!Store.Exists(row[0])||!InScope(row[0],workspace)||store.Ignored(row[0],null))continue;Int64.TryParse(row[1],out size);DateTime.TryParse(row[2],out changed);found.Add(new SearchResult(){Path=row[0].TrimEnd('\\'),Bytes=size,Changed=changed});}
                return new SearchResponse(){Provider="Everything",Message="使用 Everything 索引",Items=found.Take(limit).ToArray()};
            }
            }finally{try{if(File.Exists(export))File.Delete(export);}catch{}}
        }catch(Exception ex){error=ex.Message;}
        var pathsFound=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if(workspace==null){foreach(string p in Store.DesktopItems())pathsFound.Add(p);foreach(var g in store.Data.Groups)foreach(string p in store.Items(g,""))pathsFound.Add(p);}
        int visited=0;foreach(string dir in workspace==null?store.Data.WorkFolders:new List<string>{workspace}){
            var queue=new Queue<Tuple<string,int>>();queue.Enqueue(Tuple.Create(dir,0));
            while(queue.Count>0&&visited<5000){var current=queue.Dequeue();string[] files;try{if(!WorkspaceSelection.Available(current.Item1))continue;files=Directory.GetFileSystemEntries(current.Item1);}catch{continue;}
                foreach(string f in files){if(++visited>5000)break;try{if((File.GetAttributes(f)&FileAttributes.ReparsePoint)!=0||store.Ignored(f,null))continue;pathsFound.Add(f);if(Directory.Exists(f)&&current.Item2<3)queue.Enqueue(Tuple.Create(f,current.Item2+1));}catch{}}
            }
        }
        var matches=new List<SearchResult>();foreach(string p in pathsFound)try{if(Store.Exists(p)&&InScope(p,workspace)&&!store.Ignored(p,null)&&(!modified||File.Exists(p))&&LocalMatch(p,query))matches.Add(new SearchResult(){Path=p,Bytes=FilePresentation.Bytes(p),Changed=File.GetLastWriteTime(p)});}catch{}
        IEnumerable<SearchResult> sorted=modified?matches.OrderByDescending(p=>p.Changed):matches.OrderBy(p=>p.Name,StringComparer.CurrentCultureIgnoreCase);
        return new SearchResponse(){Provider="本机范围",Message=(error.Length>0?error+" ":"")+"限桌面/项目，最多 5000 项、4 层；支持名称、ext:、folder:、dm:today",Items=sorted.Take(limit).ToArray()};
    }
}
public partial class ManagerWindow {
    public void OpenProductivity(string page){Show();WindowState=WindowState.Normal;Activate();ShowSettingsPage(page);}
    StackPanel SearchPage(){
        var p=SettingsPage("文件搜索","按 Alt＋Space 唤起当前项目，输入即搜；也可以切换全盘文件搜索。");
        var preview=new Border(){CornerRadius=new CornerRadius(26),Padding=new Thickness(22,16,22,16),Background=Ui.Brush("#EAF0F7"),MaxWidth=680,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,12,0,20),Cursor=Cursors.Hand};
        preview.Child=new TextBlock(){Text="⌕   搜索文件、文档、图片…",FontSize=21,Foreground=Ui.Muted};preview.MouseLeftButtonUp+=delegate{ShowWorkspaceSearch();};p.Children.Add(preview);
        var actions=new WrapPanel();actions.Children.Add(Ui.Button("当前项目 · Alt＋Space",delegate{ShowWorkspaceSearch();}));actions.Children.Add(Ui.Button("搜索全盘文件",delegate{ShowWorkspaceSearch(false,true);}));p.Children.Add(actions);
        p.Children.Add(Ui.Text("↑ ↓ 选择 · Enter 打开 · Ctrl＋Enter 定位 · Esc 收起。当前项目来自 MCP 上报或搜索面板中的选择。",12,Ui.Muted));
        Section(p,"Everything 连接");Option(p,"使用 Everything 全盘文件索引",controller.Store.Data.UseEverything,delegate(bool value){controller.Store.Data.UseEverything=value;SaveSettings(false);});
        p.Children.Add(Ui.Button("启动 / 连接 Everything",delegate{try{FileSearch.Start(controller.Store.Data);SetStatus("Everything 已启动，可打开搜索条查询。");}catch(Exception ex){SetStatus(ex.Message);}}));
        p.Children.Add(Ui.Button("选择 Everything 程序",delegate{var dialog=new Microsoft.Win32.OpenFileDialog(){Title="选择 Everything.exe",Filter="Everything 程序|Everything.exe;everything.exe"};if(dialog.ShowDialog(this)!=true)return;controller.Store.Checkpoint();controller.Store.Data.EverythingPath=dialog.FileName;SaveSettings(false);}));
        p.Children.Add(Ui.Text("Everything 未连接时搜索桌面与已连接项目；项目模式保持当前目录范围。高级表达式由 Everything 提供，本机模式支持名称、文件类型和今天修改。",12,Ui.Muted));return p;
    }
    StackPanel RecentPage(){var p=SettingsPage("最近文件","最近打开来自栖桌与 Windows 最近项目；最近修改由 Everything 索引提供，未连接时使用桌面和工作目录。");var tabs=new WrapPanel();p.Children.Add(tabs);var status=Ui.Text("",12,Ui.Muted);p.Children.Add(status);var body=new StackPanel();p.Children.Add(body);int generation=0;Action opened=async delegate{int n=++generation;status.Text="读取最近打开…";State snapshot=Store.Json.Deserialize<State>(Store.Json.Serialize(controller.Store.Data));var items=await Task.Run(()=>FileActivity.Recent(new StoreView(controller.Store.Root,snapshot).Store));if(n!=generation)return;body.Children.Clear();status.Text="最近打开 · "+items.Length+" 项";foreach(var entry in items)body.Children.Add(ActivityCard(entry.Path,entry.Opened.ToString("MM-dd HH:mm")));};Action modified=async delegate{int n=++generation;status.Text="读取最近修改…";State snapshot=Store.Json.Deserialize<State>(Store.Json.Serialize(controller.Store.Data));var response=await Task.Run(()=>FileSearch.Query(new StoreView(controller.Store.Root,snapshot).Store,"",true));if(n!=generation)return;body.Children.Clear();status.Text="最近修改 · "+response.Provider+" · "+response.Items.Length+" 项";foreach(var entry in response.Items)body.Children.Add(ActivityCard(entry.Path,entry.Changed.ToString("MM-dd HH:mm")));};tabs.Children.Add(Ui.Button("最近打开",opened));tabs.Children.Add(Ui.Button("最近修改",modified));opened();return p;}
    Border ActivityCard(string path,string detail){var row=new DockPanel();var open=Ui.Button("打开",delegate{Shell.Open(path);});DockPanel.SetDock(open,Dock.Right);row.Children.Add(open);var reveal=Ui.Button("所在文件夹",delegate{Shell.Reveal(path);});DockPanel.SetDock(reveal,Dock.Right);row.Children.Add(reveal);var img=new ThumbnailImage(){FilePath=path,Width=38,Height=38,Margin=new Thickness(0,0,14,0)};DockPanel.SetDock(img,Dock.Left);row.Children.Add(img);var text=new StackPanel();text.Children.Add(Ui.Text(System.IO.Path.GetFileName(path),14,Ui.Ink));text.Children.Add(Ui.Text(detail+" · "+FilePresentation.FolderName(System.IO.Path.GetDirectoryName(path)),11,Ui.Muted));row.Children.Add(text);var card=new Border(){Child=row,CornerRadius=new CornerRadius(14),Padding=new Thickness(14),Background=Ui.Surface,Margin=new Thickness(0,0,0,10)};card.MouseLeftButtonDown+=delegate(object s,MouseButtonEventArgs e){if(e.ClickCount==2){Shell.Open(path);e.Handled=true;}};return card;}
}
// A background search uses an immutable configuration snapshot without creating or reloading directories.
public sealed class StoreView {public readonly Store Store;public StoreView(string root,State state){Store=new Store(root,false);Store.Data=state;}}
}
