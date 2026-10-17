using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;

namespace QiDesk {
public static class LayoutArchiveTests {
    public static void Run(string root,Action<bool,string> check){
        string folder=Path.Combine(root,"layout-files");Directory.CreateDirectory(folder);
        string photo=Path.Combine(folder,"photo.PNG");File.WriteAllText(photo,"x");
        string report=Path.Combine(folder,"report.pdf");File.WriteAllText(report,"x");
        string note=Path.Combine(folder,"note.txt");File.WriteAllText(note,"x");
        string zip=Path.Combine(folder,"bundle.zip");File.WriteAllText(zip,"x");
        string sub=Path.Combine(folder,"sub");Directory.CreateDirectory(sub);
        string loose=Path.Combine(folder,"unknown.xyz");File.WriteAllText(loose,"x");

        var store=new Store(Path.Combine(root,"layout-state","store"));
        store.Data.Groups.Clear();store.Data.Rules.Clear();store.Data.Excluded.Clear();
        var controller=new Controller(store);
        var config=SampleConfig();
        string error;
        LayoutArchives.Save(store,config);
        check(File.Exists(LayoutArchives.ConfigPath(store)),"layout config is written to desktop-layouts.json");
        var loaded=LayoutArchives.Load(store,out error);
        check(loaded!=null&&error.Length==0&&loaded.Archives.Count==2&&loaded.Archives[0].Partitions.Count==4,"layout config round-trips through json ["+error+"]");
        string pretty=JsonPretty.Format(Store.Json.Serialize(loaded));
        check(Store.Json.Deserialize<Dictionary<string,object>>(pretty)!=null,"pretty printed layout json is still valid json");

        // unknown fields and bad targets are rejected before touching runtime state.
        string path=LayoutArchives.ConfigPath(store);string good=File.ReadAllText(path);
        File.WriteAllText(path,good.Replace("\"schema\"","\"unexpected\":true,\"schema\""));
        LayoutArchives.Load(store,out error);
        check(error.Contains("未知字段"),"layout config rejects unknown fields");
        File.WriteAllText(path,good);
        var bad=Store.Json.Deserialize<LayoutConfig>(good);bad.Archives[0].Rules[0].Target="p_missing";
        bool rejected=false;try{LayoutArchives.Save(store,bad);}catch(InvalidDataException){rejected=true;}
        check(rejected,"layout config rejects a rule target that does not exist");
        File.WriteAllText(path,good);

        var candidates=new[]{photo,report,note,zip,sub,loose};
        var preview=LayoutArchives.Apply(controller,"work",true,true,true,candidates);
        check(preview.Preview&&preview.Assigned==4&&preview.Unmatched==2,"layout preview computes matches without opening partitions");
        check(store.Data.Groups.Count==0,"layout preview does not mutate the running state");
        check(File.Exists(photo)&&File.Exists(report)&&File.Exists(note)&&File.Exists(zip),"layout preview never moves or deletes files");

        var applied=LayoutArchives.Apply(controller,"work",true,true,false,candidates);
        check(!applied.Preview&&store.Data.Groups.Count==4,"layout apply creates one partition per archive entry");
        check(store.Data.Groups.Any(g=>g.ArchiveKey=="p_images"&&g.Items.Contains(photo))&&store.Data.Groups.Any(g=>g.ArchiveKey=="p_docs"&&g.Items.Contains(report)&&g.Items.Contains(note))&&store.Data.Groups.Any(g=>g.ArchiveKey=="p_zip"&&g.Items.Contains(zip)),"first matching extension rule assigns each file to the intended partition");
        check(store.Data.Groups.Where(g=>g.ArchiveKey!="p_inbox").SelectMany(g=>g.Items).All(p=>!String.Equals(p,sub,StringComparison.OrdinalIgnoreCase)&&!String.Equals(p,loose,StringComparison.OrdinalIgnoreCase)),"unmatched files and folders stay out of rule partitions");
        check(store.Data.ActiveLayoutArchiveId=="work"&&store.Data.LastAppliedLayoutRevision==1,"applying an archive records the active archive and revision");
        check(File.Exists(photo)&&File.Exists(report)&&File.Exists(note)&&File.Exists(zip),"layout apply never moves or deletes files");
        check(store.Data.Groups.Where(g=>g.ArchiveKey=="p_docs"||g.ArchiveKey=="p_zip").All(g=>g.Width>0&&g.Height>0&&g.X>=0&&g.Y>=0),"partitions without coordinates are placed by the layout template");
        var placed=store.Data.Groups.Where(g=>g.ArchiveKey=="p_docs"||g.ArchiveKey=="p_zip").Select(g=>new Rect(g.X,g.Y,g.Width,g.Height)).ToArray();
        check(placed.Length==2&&Rect.Intersect(placed[0],placed[1]).IsEmpty,"template placements do not overlap");
        check(store.Data.Groups.Single(g=>g.ArchiveKey=="p_images").X==40&&store.Data.Groups.Single(g=>g.ArchiveKey=="p_images").Y==60,"explicit coordinates override the layout template");
        var again=LayoutArchives.Apply(controller,"work",true,true,true,candidates);
        check(again.PartitionsAdded==0&&again.PartitionsRemoved==0&&again.Moved==0,"re-previewing the same archive sees no partition or item changes");

        // layout-only apply keeps item membership and moves only geometry.
        var moved=SampleConfig();moved.Archives[0].Partitions.Single(p=>p.Id=="p_images").X=500;moved.Revision=2;
        LayoutArchives.Save(store,moved);
        int before=store.Data.Groups.Sum(g=>g.Items.Count);int imagesBefore=store.Data.Groups.Single(g=>g.ArchiveKey=="p_images").Items.Count;
        var layoutOnly=LayoutArchives.Apply(controller,"work",false,true,false,candidates);
        check(layoutOnly.Assigned==before&&store.Data.Groups.Single(g=>g.ArchiveKey=="p_images").Items.Count==imagesBefore,"layout-only apply preserves item membership [assigned="+layoutOnly.Assigned+" before="+before+" images="+store.Data.Groups.Single(g=>g.ArchiveKey=="p_images").Items.Count+"/"+imagesBefore+"]");
        check(store.Data.Groups.Single(g=>g.ArchiveKey=="p_images").X==500,"layout-only apply still moves partition geometry");

        // fallback partition can collect unmatched files; default stays on the desktop.
        var fallback=SampleConfig();fallback.Archives[0].Fallback=new LayoutFallback(){Target="p_inbox",Unmatched="partition"};fallback.Revision=3;
        LayoutArchives.Save(store,fallback);
        var fallbackResult=LayoutArchives.Apply(controller,"work",true,true,false,candidates);
        check(store.Data.Groups.Single(g=>g.ArchiveKey=="p_inbox").Items.Contains(sub)&&store.Data.Groups.Single(g=>g.ArchiveKey=="p_inbox").Items.Contains(loose),"an explicit fallback partition collects unmatched items");
        check(fallbackResult.Assigned==6,"fallback apply assigns every matched and unmatched item");

        // export current writes a readable archive and keeps the file parseable.
        string exportRoot=Path.Combine(root,"layout-export");var exportStore=new Store(exportRoot);
        exportStore.Data.Groups.Clear();exportStore.Data.Rules.Clear();
        exportStore.Data.Groups.Add(new Group(){Name="文档",X=30,Y=40,Width=360,Height=300,Items=new List<string>{report}});
        exportStore.Data.Rules.Add(new Rule(){Name="文档",Extensions="pdf",GroupId=exportStore.Data.Groups[0].Id});
        LayoutArchives.ExportCurrent(exportStore,"work","导出测试");
        var exported=LayoutArchives.Load(exportStore,out error);
        check(exported!=null&&exported.Archives.Count==1&&exported.Archives[0].Partitions[0].Name=="文档"&&exported.Archives[0].Partitions[0].Source.Type=="rules"&&exported.Archives[0].Rules.Count==1,"export current produces an archive with partitions and rules");

        // Invalid json / duplicate ids / bad geometry are rejected.
        var duplicate=SampleConfig();duplicate.Archives[0].Partitions[1].Id=duplicate.Archives[0].Partitions[0].Id;
        bool duplicateRejected=false;try{LayoutArchives.Save(store,duplicate);}catch(InvalidDataException){duplicateRejected=true;}
        check(duplicateRejected,"duplicate partition ids are rejected");
        var geometry=SampleConfig();geometry.Archives[0].Partitions[0].Width=0;geometry.Archives[0].Partitions[0].Height=200;
        bool geometryRejected=false;try{LayoutArchives.Save(store,geometry);}catch(InvalidDataException){geometryRejected=true;}
        check(geometryRejected,"partial geometry is rejected instead of silently guessed");
    }

    static LayoutConfig SampleConfig(){
        var archive=new LayoutArchive(){Id="work",Name="工作桌面",Pages=1,Layout=new LayoutTemplate(){Template="three-columns",Gap=16,Margin=40},Fallback=new LayoutFallback(){Unmatched="desktop"}};
        archive.Partitions.Add(new LayoutPartition(){Id="p_images",Name="图片",Page=0,X=40,Y=60,Width=320,Height=260,Color="#77BFA3",Source=new LayoutSource(){Type="rules"}});
        archive.Partitions.Add(new LayoutPartition(){Id="p_docs",Name="文档",Page=0,Color="#92AAE5",Source=new LayoutSource(){Type="rules"}});
        archive.Partitions.Add(new LayoutPartition(){Id="p_zip",Name="压缩包",Page=0,Color="#D9AA75",Source=new LayoutSource(){Type="rules"}});
        archive.Partitions.Add(new LayoutPartition(){Id="p_inbox",Name="收件箱",Page=0,Color="#81909E",Source=new LayoutSource(){Type="rules"}});
        archive.Rules.Add(new LayoutRule(){Id="r_images",Name="图片",Target="p_images",Match=new LayoutMatch(){Kind="file",Extensions=new List<string>{"png","jpg"}}});
        archive.Rules.Add(new LayoutRule(){Id="r_docs",Name="文档",Target="p_docs",Match=new LayoutMatch(){Kind="file",Extensions=new List<string>{"pdf","txt"}}});
        archive.Rules.Add(new LayoutRule(){Id="r_zip",Name="压缩包",Target="p_zip",Match=new LayoutMatch(){Kind="file",Extensions=new List<string>{"zip"}}});
        var config=new LayoutConfig(){Active="work",Revision=1};
        config.Archives.Add(archive);
        config.Archives.Add(new LayoutArchive(){Id="game",Name="游戏桌面",Pages=1,Layout=new LayoutTemplate(){Template="focus-inbox"},Partitions=new List<LayoutPartition>{new LayoutPartition(){Id="p_game",Name="游戏",Source=new LayoutSource(){Type="rules"}}},Rules=new List<LayoutRule>()});
        return config;
    }
}
}
