from pathlib import Path
root=Path(__file__).resolve().parents[1]
p=root/'src/Productivity.cs';s=p.read_text(encoding='utf-8-sig')
changes=[
('int limit=100){limit=', 'int limit=100,string workspaceRoot=null){if(workspaceRoot!=null&&!WorkspaceConnections.SafeDirectory(workspaceRoot))throw new InvalidDataException("搜索目录暂不可用。");limit='),
('Quote((modified?"file: ":"")+(query??""))','Quote((workspaceRoot==null?"":"path:\\""+IgnoreRule.Canonical(workspaceRoot)+"\\\\\\\" ")+(modified?"file: ":"")+(query??""))'),
('!Store.Exists(row[0])||store.Ignored(row[0],null)','!Store.Exists(row[0])||store.Ignored(row[0],null)||(workspaceRoot!=null&&!WorkDetection.Under(row[0],workspaceRoot))'),
('foreach(string p in Store.DesktopItems())found.Add(p);foreach(var g in store.Data.Groups)foreach(string p in store.Items(g,""))found.Add(p);','if(workspaceRoot==null){foreach(string p in Store.DesktopItems())found.Add(p);foreach(var g in store.Data.Groups)foreach(string p in store.Items(g,""))found.Add(p);}'),
('foreach(string dir in store.Data.WorkFolders)','foreach(string dir in workspaceRoot==null?store.Data.WorkFolders:new List<string>{workspaceRoot})'),
('(String.IsNullOrWhiteSpace(query)||System.IO.Path.GetFileName(p).IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0)','MatchesLocal(p,query)'),
('public static List<string[]> Csv','public static bool MatchesLocal(string path,string query){string name=query??"";var ext=System.Text.RegularExpressions.Regex.Match(name,@"ext:([^ ]+)",System.Text.RegularExpressions.RegexOptions.IgnoreCase);if(ext.Success){if(!ext.Groups[1].Value.Split(\';\').Any(e=>String.Equals("."+e.TrimStart(\'.\'),System.IO.Path.GetExtension(path),StringComparison.OrdinalIgnoreCase)))return false;name=name.Replace(ext.Value,"");}if(name.IndexOf("folder:",StringComparison.OrdinalIgnoreCase)>=0){if(!Directory.Exists(path))return false;name=System.Text.RegularExpressions.Regex.Replace(name,"folder:","",System.Text.RegularExpressions.RegexOptions.IgnoreCase);}if(name.IndexOf("dm:today",StringComparison.OrdinalIgnoreCase)>=0){if(File.GetLastWriteTime(path).Date!=DateTime.Today)return false;name=System.Text.RegularExpressions.Regex.Replace(name,"dm:today","",System.Text.RegularExpressions.RegexOptions.IgnoreCase);}name=name.Trim();return name.Length==0||System.IO.Path.GetFileName(path).IndexOf(name,StringComparison.OrdinalIgnoreCase)>=0;}public static List<string[]> Csv'),
('var items=await Task.Run(()=>FileActivity.Recent(controller.Store));','var state=Store.Json.Deserialize<State>(Store.Json.Serialize(controller.Store.Data));var items=await Task.Run(()=>FileActivity.Recent(new StoreView(controller.Store.Root,state).Store));'),
('var response=await Task.Run(()=>FileSearch.Query(controller.Store,"",true));','var state=Store.Json.Deserialize<State>(Store.Json.Serialize(controller.Store.Data));var response=await Task.Run(()=>FileSearch.Query(new StoreView(controller.Store.Root,state).Store,"",true));'),
]
for old,new in changes:
 assert old in s,old[:80];s=s.replace(old,new)
p.write_text(s,encoding='utf-8-sig')
print('Scoped search supports concurrent workspace search changes')
