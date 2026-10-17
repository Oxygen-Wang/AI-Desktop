using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace QiDesk {
public static class PartitionTabTests {
    [DllImport("user32.dll")] static extern void mouse_event(uint flags,uint dx,uint dy,uint data,UIntPtr extra);
    public static void Run(string root,Action<bool,string> check){
        string one=Path.Combine(root,"tab-one");Directory.CreateDirectory(one);
        string two=Path.Combine(root,"tab-two");Directory.CreateDirectory(two);
        string oneFile=Path.Combine(one,"需求说明.md");File.WriteAllText(oneFile,"x");
        string twoFile=Path.Combine(two,"照片.png");File.WriteAllText(twoFile,"x");
        string report=Path.Combine(root,"报告.pdf");File.WriteAllText(report,"x");
        var store=new Store(Path.Combine(root,"tab-state"));
        store.Data.Groups.Clear();
        var host=new Group(){Name="文档",Kind="普通",Width=560,Height=360,Page=0,Folders=new List<string>{one,two},FolderIndex=0};
        var picture=new Group(){Name="图片",HostGroupId=host.Id,Page=0};
        var single=new Group(){Name="映射",Kind="文件夹",Folder=one,Folders=new List<string>{one},FolderIndex=0};
        store.Data.Groups.Add(host);store.Data.Groups.Add(picture);store.Data.Groups.Add(single);
        store.Data.Pages=1;store.Data.Page=0;

        var tabs=PartitionTabs.Of(store.Data,host);
        check(tabs.Count==4&&tabs[0].IsSelf&&tabs[1].IsFolder&&tabs[2].IsFolder&&tabs[3].IsMember,"one tab list covers the section itself its folder mappings and merged sections");
        check(tabs[0].Title=="文档"&&tabs[1].Title=="tab-one"&&tabs[2].Title=="tab-two"&&tabs[3].Title=="图片","all tab kinds derive their title from the same model");
        check(tabs[1].Active&&!tabs[0].Active&&!tabs[3].Active,"the current folder tab is the one active tab");
        host.ActiveGroupId=picture.Id;
        tabs=PartitionTabs.Of(store.Data,host);
        check(!tabs.Where(t=>t.IsSelf).Any(t=>t.Active)&&tabs.Single(t=>t.IsMember).Active,"a merged section is active exactly like a folder tab");
        host.ActiveGroupId=host.Id;host.FolderIndex=-1;
        tabs=PartitionTabs.Of(store.Data,host);
        check(tabs[0].Active&&tabs.Where(t=>t.IsFolder).All(t=>!t.Active),"the self tab returns after leaving folder view");
        var singleTabs=PartitionTabs.Of(store.Data,single);
        check(singleTabs.Count==1&&singleTabs[0].IsFolder&&singleTabs[0].Active,"a single folder section uses its folder tab instead of a duplicate self tab");

        var nestedHost=new Group(){Name="外层",Kind="普通",Folders=new List<string>{one},FolderIndex=0};
        var nestedMember=new Group(){Name="内层",Kind="普通",HostGroupId=nestedHost.Id,Folders=new List<string>{two},FolderIndex=0};
        store.Data.Groups.Add(nestedHost);store.Data.Groups.Add(nestedMember);nestedHost.ActiveGroupId=nestedMember.Id;
        var nestedTabs=PartitionTabs.Of(store.Data,nestedHost);
        check(nestedTabs.Any(t=>t.IsFolder&&t.Owner==nestedMember&&t.Active),"a merged folder section exposes its own folder tabs when active");
        check(nestedTabs.Any(t=>t.IsFolder&&t.Owner==nestedHost&&!t.Active),"the host folder tabs stay reachable while a merged section is active");
        check(PartitionTabs.FolderOwner(store.Data,nestedHost)==nestedMember,"the active member owns the folder tab source");
        nestedHost.ActiveGroupId=nestedHost.Id;
        check(PartitionTabs.FolderOwner(store.Data,nestedHost)==nestedHost,"returning to the self tab restores the host folder tabs");

        var scoped=new IgnoreRule(){Kind="Git",Pattern="*.tmp"};
        if(host.FolderIgnoreRules==null)host.FolderIgnoreRules=new Dictionary<string,List<IgnoreRule>>(StringComparer.OrdinalIgnoreCase);
        host.FolderIgnoreRules[one]=new List<IgnoreRule>{scoped};
        var controller=new Controller(store);
        var detached=controller.DetachFolderTab(host,host,one,new Point(900,140));
        check(detached!=null&&detached.Kind=="文件夹"&&detached.Folder==one&&detached.Folders.Contains(one)&&!host.Folders.Contains(one),"dragging a folder tab out creates an independent folder section");
        check(detached.IgnoreRules.Count==1&&detached.IgnoreRules[0].Pattern=="*.tmp"&&!host.FolderIgnoreRules.ContainsKey(one),"folder scoped ignore rules travel with the detached tab");
        check(detached.Color==host.Color&&detached.CornerStyle==host.CornerStyle&&detached.IconSize==host.IconSize,"a detached folder tab keeps the source appearance");
        check(store.Items(detached,"").Contains(oneFile),"the detached folder section shows the same folder content");
        check(File.Exists(oneFile)&&File.Exists(twoFile),"dragging tabs never moves or deletes real files");

        var target=new Group(){Name="收纳区",Kind="普通",Width=420,Height=320,Page=0};
        store.Data.Groups.Add(target);
        check(controller.MoveFolderTab(host,host,two,target)&&!host.Folders.Contains(two)&&target.Folders.Contains(two),"dragging a folder tab onto another section moves the mapping");
        check(target.FolderIndex==target.Folders.IndexOf(two)&&target.ActiveGroupId==target.Id,"a moved folder tab becomes active in its new section");
        check(File.Exists(twoFile),"moving a folder tab keeps the folder on disk");
        var movedTabs=PartitionTabs.Of(store.Data,target);
        check(movedTabs.Any(t=>t.IsFolder&&t.Active&&t.Title=="tab-two"),"the target section exposes the moved folder as an equal tab");

        var legacy=new State();legacy.Version=8;
        var work=new Group(){Name="图片与工作文档",Kind="工作",Items=new List<string>{report,twoFile}};
        legacy.Groups.Add(work);
        Store.Validate(legacy);
        var legacyTabs=PartitionTabs.Of(legacy,work);
        check(work.Kind=="普通"&&legacyTabs.Count==2&&legacyTabs.Any(t=>t.IsMember&&t.Title=="图片"),"legacy work sections become ordinary sections with a picture tab");
        check(work.Items.Contains(report)&&legacy.Groups.Single(g=>g.HostGroupId==work.Id).Items.Contains(twoFile),"legacy work section migration preserves document and picture membership");
        Store.Validate(legacy);
        check(legacy.Groups.Count(g=>g.HostGroupId==work.Id)==1,"work section migration is idempotent across restarts");

        var loaded=new Store(store.Root);
        var loadedHost=loaded.Data.Groups.Single(g=>g.Id==host.Id);
        check(loadedHost.Folders.Count==0&&loaded.Data.Groups.Any(g=>g.Id==detached.Id&&g.Folder==one),"tab relationships survive a restart");
    }

    public static void Native(string root,Action<bool,string> check){
        var store=new Store(Path.Combine(root,"tab-native"));
        string folder=Path.Combine(root,"标签资料");Directory.CreateDirectory(folder);
        string file=Path.Combine(folder,"说明.txt");File.WriteAllText(file,"x");
        var host=new Group(){Name="分区标签",Kind="普通",X=120,Y=120,Width=520,Height=320,Page=0};
        host.Folders.Add(folder);host.FolderIndex=0;
        var child=new Group(){Name="图片标签",HostGroupId=host.Id,Page=0};
        store.Data.Groups=new List<Group>{host,child};store.Data.Pages=1;store.Data.Page=0;
        var controller=new Controller(store){Running=true,Docked=false};
        var manager=new ManagerWindow(controller);
        try{
            controller.Rebuild();
            var window=controller.Windows.Single(w=>w.Group.Id==host.Id);
            window.UpdateLayout();
            var texts=TextNodes(window).ToArray();
            check(texts.Any(t=>t.Text=="分区标签"),"the section itself is shown as a tab");
            check(texts.Any(t=>t.Text==FilePresentation.FolderName(folder)),"a folder mapping is shown as a tab in the same bar");
            check(texts.Any(t=>t.Text=="图片标签"),"a merged section is shown as a tab in the same bar");
            DragTabOut(window,"图片标签");
            check(store.Data.Groups.Single(g=>g.Id==child.Id).HostGroupId=="","dragging a merged tab out restores an independent section");
            check(controller.Windows.Any(w=>w.Group.Id==child.Id)&&controller.Windows.Any(w=>w.Group.Id==host.Id),"both sections stay on the desktop after the tab is dragged out");
            check(File.Exists(file)&&store.Data.Groups.Where(g=>g.Id==host.Id).All(g=>g.Folders.Count==1),"dragging a section tab out leaves folders and files untouched");
        }finally{manager.Quit();}
    }

    static void DragTabOut(FenceWindow window,string title){
        var original=System.Windows.Forms.Cursor.Position;
        bool pressed=false;
        try{
            Shell.Float(window);window.Topmost=true;Shell.BeginInlineInput(window);window.UpdateLayout();
            var node=TextNodes(window).First(t=>t.Text==title);
            Point location=node.PointToScreen(new Point(Math.Min(8,node.ActualWidth/2),Math.Min(8,node.ActualHeight/2)));
            System.Windows.Forms.Cursor.Position=new System.Drawing.Point((int)location.X,(int)location.Y);
            PumpFor(50);
            mouse_event(2,0,0,0,UIntPtr.Zero);pressed=true;PumpFor(50);
            System.Windows.Forms.Cursor.Position=new System.Drawing.Point((int)location.X+280,(int)location.Y+180);
            PumpFor(90);
            mouse_event(4,0,0,0,UIntPtr.Zero);pressed=false;PumpFor(120);
        }finally{
            if(pressed)mouse_event(4,0,0,0,UIntPtr.Zero);
            System.Windows.Forms.Cursor.Position=original;
        }
    }
    static IEnumerable<TextBlock> TextNodes(DependencyObject node){
        var text=node as TextBlock;if(text!=null)yield return text;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)foreach(var child in TextNodes(VisualTreeHelper.GetChild(node,i)))yield return child;
    }
    static void PumpFor(int milliseconds){var end=DateTime.UtcNow.AddMilliseconds(milliseconds);PumpUntil(()=>DateTime.UtcNow>=end,milliseconds+120);}
    static void PumpUntil(Func<bool> ready,int milliseconds){
        var end=DateTime.UtcNow.AddMilliseconds(milliseconds);
        while(!ready()&&DateTime.UtcNow<end){var frame=new DispatcherFrame();var timer=new DispatcherTimer(){Interval=TimeSpan.FromMilliseconds(20)};timer.Tick+=delegate{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);}
    }
}
}
