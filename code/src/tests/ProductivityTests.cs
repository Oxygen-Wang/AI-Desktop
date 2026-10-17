using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;

namespace QiDesk {
public static class ProductivityTests {
    static Dictionary<string,object> Request(string name,Dictionary<string,object> args){return new Dictionary<string,object>{{"name",name},{"arguments",args}};}
    public static void Run(string root,Action<bool,string> check){string folder=Path.Combine(root,"productivity"),dest=Path.Combine(folder,"target"),nested=Path.Combine(folder,"nested");Directory.CreateDirectory(dest);Directory.CreateDirectory(nested);string file=Path.Combine(folder,"photo.png"),doc=Path.Combine(folder,"notes.txt");File.WriteAllText(file,"image fixture bytes");File.WriteAllText(doc,"1234567890");
        check(DragTransfer.CanCopy(new[]{file},dest)&&!DragTransfer.CanCopy(new[]{file},doc),"drag accepts a folder destination and rejects a file destination");check(!DragTransfer.CanCopy(new[]{folder},nested)&&!DragTransfer.CanCopy(new[]{folder},folder),"drag rejects recursive folder copy and self copy");
        var s=new Store(Path.Combine(root,"productivity-state"));var g=s.Data.Groups[0];g.Items.AddRange(new[]{doc,file});g.Sort="大小";g.SortDescending=true;check(s.Items(g,"").First()==file,"size sorting compares byte counts rather than formatted labels");g.SortDescending=false;check(s.Items(g,"").First()==doc,"sorting direction reverses the same file set");check(s.ItemsAt(g,"",dest).Count()==0,"plain group detail browser lists current folder rather than original membership");
        s.Data.UseEverything=false;s.Data.WorkFolders.Add(folder);var results=FileSearch.Query(s,"notes");check(results.Provider=="本机范围"&&results.Items.Any(x=>x.Path==doc),"search falls back to selected workspace files without Everything");
        var csv=FileSearch.Csv("Filename,Size,Date Modified\r\n\"D:\\a,b\\a\"\"b.txt\",10,2026-10-13T12:00:00\r\n");check(csv.Count==2&&csv[1][0]=="D:\\a,b\\a\"b.txt"&&csv[1][1]=="10","Everything CSV preserves commas and escaped quotes in filenames");check(FileSearch.Quote("x\"y\\")=="\"x\\\"y\\\\\"","Windows argument quoting preserves embedded quotes and trailing slashes");
        var c=new Controller(s);var update=Request("qidesk_update_group",new Dictionary<string,object>{{"groupId",g.Id},{"settings",new Dictionary<string,object>{{"Color","#D79CAF"},{"BackgroundOpacity",0.0},{"TintStrength",.8},{"ViewMode","详细信息"},{"Sort","大小"},{"SortDescending",true}}}});McpBridge.Execute(c,update);var loaded=new Store(s.Root);check(loaded.Data.Groups[0].Color=="#D79CAF"&&loaded.Data.Groups[0].BackgroundOpacity==0&&loaded.Data.Groups[0].ViewMode=="详细信息","AI appearance view and sorting changes persist across restart");string before=Store.Json.Serialize(s.Data);bool failed=false;try{McpBridge.Execute(c,Request("qidesk_update_group",new Dictionary<string,object>{{"groupId",g.Id},{"settings",new Dictionary<string,object>{{"Color","#FFFFFF"},{"IconSize",999}}}}));}catch{failed=true;}Func<string,string> normalize=delegate(string json){return CanonicalJson(Store.Json.DeserializeObject(json));};string normalizedBefore=normalize(before),normalizedAfter=normalize(Store.Json.Serialize(s.Data));check(failed,"AI invalid setting is rejected");check(normalizedAfter==normalizedBefore,"AI invalid setting rolls back the whole requested change");
        failed=false;try{McpBridge.Execute(c,Request("qidesk_update_settings",new Dictionary<string,object>{{"settings",new Dictionary<string,object>{{"ClipboardEnabled",true}}}}));}catch{failed=true;}check(failed,"AI cannot enable a setting that does not exist");
        var old=new State(){Version=2,HideOriginalIcons=false,PortalNavigate=false};old.Groups.Add(new Group(){TintStrength=.18});Store.Validate(old);check(!old.HideOriginalIcons&&old.PortalNavigate&&old.Groups[0].TintStrength>.5,"core migration keeps Explorer desktop visible and restores group colors");old.HideOriginalIcons=false;old.PortalNavigate=false;Store.Validate(old);check(!old.HideOriginalIcons&&!old.PortalNavigate,"subsequent validation preserves explicit user preferences");
        var brush=(SolidColorBrush)Appearance.Background("#D79CAF",.8,.7);check(brush.Color.R>100&&brush.Color.A>150,"appearance preview visibly uses selected tint and opacity");
        FileActivity.Initialize(s.Root);FileActivity.Record(doc);check(FileActivity.Recent(s).Any(e=>e.Path==doc),"recent opened files include recorded application openings");FileActivity.Initialize(s.Root);check(FileActivity.Recent(s).Any(e=>e.Path==doc),"recent file activity survives reload");
    }
    // JSON member order is not part of state identity. Reflection cache changes can
    // reorder serializer output after a property is inspected by an AI request.
    static string CanonicalJson(object value){
        var dictionary=value as Dictionary<string,object>;
        if(dictionary!=null){
            return "{"+String.Join(",",dictionary.OrderBy(pair=>pair.Key,StringComparer.Ordinal)
                .Select(pair=>Store.Json.Serialize(pair.Key)+":"+CanonicalJson(pair.Value)))+"}";
        }
        var sequence=value as System.Collections.IEnumerable;
        if(sequence!=null&&!(value is string)){
            return "["+String.Join(",",sequence.Cast<object>().Select(CanonicalJson))+"]";
        }
        return Store.Json.Serialize(value);
    }
    public static void Native(string root,Window owner,Action<bool,string> check){string folder=Path.Combine(root,"native-drag"),dest=Path.Combine(folder,"copy-target"),moved=Path.Combine(folder,"move-target");Directory.CreateDirectory(dest);Directory.CreateDirectory(moved);string file=Path.Combine(folder,"drag.txt");File.WriteAllText(file,"native drag preserve bytes");check(DragTransfer.Copy(owner,new[]{file},dest,false)&&File.Exists(file)&&File.ReadAllText(Path.Combine(dest,"drag.txt"))=="native drag preserve bytes","actual drag copy creates destination file while preserving source bytes");check(DragTransfer.Copy(owner,new[]{file},moved,true)&&!File.Exists(file)&&File.ReadAllText(Path.Combine(moved,"drag.txt"))=="native drag preserve bytes","actual Shift drag moves fixture through Windows file operations");var state=new State();state.Groups.Add(new Group(){Name="布局预览",X=40,Y=60,Items=new List<string>{Path.Combine(dest,"drag.txt")}});var preview=SnapshotPreview.Render(state);byte[] bytes=new byte[preview.PixelWidth*preview.PixelHeight*4];preview.CopyPixels(bytes,preview.PixelWidth*4,0);check(bytes.Where((b,i)=>i%4==3).Any(b=>b==255)&&bytes.Distinct().Count()>16,"snapshot preview produces actual colored layout and icon content");}
}
}
