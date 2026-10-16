using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;

namespace QiDesk {
public static class ReferenceTests {
    public static void Run(string root,Action<bool,string> check){
        var prefix=new Rule(){Contains="report",NameMode="开头",Extensions="pdf"};check(prefix.Matches(Path.Combine(root,"REPORT-2026.PDF"))&&!prefix.Matches(Path.Combine(root,"xreport.pdf"))&&!prefix.Matches(Path.Combine(root,"report.png")),"prefix name condition combines with file type");
        var suffix=new Rule(){Contains="final.pdf",NameMode="结尾"};check(suffix.Matches(Path.Combine(root,"report-FINAL.PDF"))&&!suffix.Matches(Path.Combine(root,"final.pdf.bak")),"suffix name rule excludes similar trailing names");
        var kind=new Rule(){Kind="文件夹"};check(kind.Matches(root)&&!kind.Matches(Path.Combine(root,"REPORT.PDF")),"folder rule excludes regular files");
        string child=Path.Combine(root,"child"),nested=Path.Combine(child,"nested");Directory.CreateDirectory(nested);var portal=new PortalSession(root);check(portal.Navigate(child)&&portal.Navigate(nested)&&portal.Back()&&portal.Current.TrimEnd('\\')==child,"portal navigation keeps back history");check(portal.Parent()&&!portal.Parent()&&!portal.Navigate(Path.GetTempPath()),"portal parent navigation stays within mapped root");
        var snap=new Group(){X=332,Y=67,Width=320,Height=340};Layout.Snap(snap,new[]{new Group(){X=0,Y=64,Width=320,Height=340}},8,16);check(snap.X==336&&snap.Y==64&&snap.Height==336,"layout snap preserves configured neighbor gap");
        var scaled=new Group(){X=-1000,Y=80,Width=320,Height=240};Layout.ScaleGroups(new[]{scaled},new List<Rect>{new Rect(-1600,0,1600,900)},new List<Rect>{new Rect(-2000,0,2000,1125)});check(scaled.X==-1250&&scaled.Y==100&&scaled.Width==400&&scaled.Height==300,"layout scaling respects negative monitor origins");
        var selection=SelectionOverlay.Selection(new Point(400,300),new Point(100,50));check(selection.X==100&&selection.Y==50&&selection.Width==300&&selection.Height==250,"reverse rectangle drag produces valid selection bounds");
        var store=new Store(Path.Combine(root,"settings-tests"));var group=store.Data.Groups[0];string groupId=group.Id;store.RecordLayout("monitor-A");group.X=800;store.RecordLayout("monitor-B");check(store.RestoreLayout("monitor-A")&&group.X==40&&group.Id==groupId&&group.Name=="应用与入口","monitor profile restoration changes geometry without replacing group identity");store.Save();var reload=new Store(store.Root);check(reload.RestoreLayout("monitor-B")&&reload.Data.Groups[0].X==800,"monitor layout profiles survive restart");
        store.Data.AutoDailySnapshot=true;store.Data.SnapshotKeepDays=3;string manual=store.Snapshot();for(int i=1;i<=4;i++)store.DailySnapshot(new DateTime(2026,1,i));check(Directory.GetFiles(store.SnapshotRoot,"auto-*.json").Length==3&&File.Exists(manual)&&!File.Exists(Path.Combine(store.SnapshotRoot,"auto-20260101.json")),"daily snapshot retention leaves manual snapshots intact");
        string daily=Path.Combine(store.SnapshotRoot,"auto-20260104.json"),saved=File.ReadAllText(daily);store.Data.Groups[0].Name="later edit";store.DailySnapshot(new DateTime(2026,1,4));check(File.ReadAllText(daily)==saved,"same-day automatic snapshot is not overwritten");
        store.Data.Groups[0].TitleMode="鼠标悬停";store.Data.Groups[0].IconSize=48;store.Data.Groups[0].CornerStyle="方角";store.Data.ShowFileExtensions=true;store.Data.PortalNavigate=false;store.Data.LayoutMode="按显示器恢复";store.Save();reload=new Store(store.Root);check(reload.Data.Groups[0].TitleMode=="鼠标悬停"&&reload.Data.Groups[0].IconSize==48&&reload.Data.Groups[0].CornerStyle=="方角"&&reload.Data.ShowFileExtensions&&!reload.Data.PortalNavigate&&reload.Data.LayoutMode=="按显示器恢复","reference interface preferences survive restart");
    }
}
}
