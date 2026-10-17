using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using QiDesk;

// Documentation-only renderer. It uses isolated sample files and the compiled
// application's real controls; it never attaches panels to the user's desktop.
class ReadmeMedia {
    static readonly JavaScriptSerializer Json=new JavaScriptSerializer();
    static Dictionary<string,object> copy;
    static string output,fixtures;
    static string Text(string key){return Convert.ToString(copy[key]);}
    static string[] Words(string key){return ((System.Collections.IEnumerable)copy[key]).Cast<object>().Select(Convert.ToString).ToArray();}
    static SolidColorBrush Ink(string value){return new SolidColorBrush((Color)ColorConverter.ConvertFromString(value));}
    static readonly Typeface Font=new Typeface(new FontFamily("Microsoft YaHei UI"),FontStyles.Normal,FontWeights.Normal,FontStretches.Normal);
    static void Label(DrawingContext dc,string text,double x,double y,double size,string color){
        var formatted=new FormattedText(text,CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,Font,size,Ink(color));
        formatted.LineHeight=size*1.5;dc.DrawText(formatted,new Point(x,y));
    }
    static RenderTargetBitmap Render(Visual visual,int width,int height){
        var result=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);result.Render(visual);return result;
    }
    static BitmapSource Snapshot(FrameworkElement control,int width,int height){
        control.Measure(new Size(width,height));control.Arrange(new Rect(0,0,width,height));control.UpdateLayout();
        return Render(control,width,height);
    }
    static BitmapSource CatFrame(VectorCatVisual cat){
        cat.Measure(new Size(100,112));cat.Arrange(new Rect(0,0,100,112));cat.UpdateLayout();
        var bitmap=new RenderTargetBitmap(200,224,192,192,PixelFormats.Pbgra32);bitmap.Render(cat);return bitmap;
    }
    static void Save(BitmapSource bitmap,string name){
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using(var stream=File.Create(Path.Combine(output,name)))encoder.Save(stream);
    }
    static void Pump(){
        var frame=new DispatcherFrame();var timer=new DispatcherTimer(){Interval=TimeSpan.FromMilliseconds(45)};
        timer.Tick+=delegate{timer.Stop();frame.Continue=false;};timer.Start();Dispatcher.PushFrame(frame);
    }
    static string Sample(string name){string path=Path.Combine(fixtures,name);File.WriteAllText(path,"README sample fixture");return path;}
    static string Picture(string name,int variant){
        var visual=new DrawingVisual();
        using(var dc=visual.RenderOpen()){
            dc.DrawRectangle(new LinearGradientBrush((Color)ColorConverter.ConvertFromString(variant==0?"#2D667C":"#EABFA0"),
                (Color)ColorConverter.ConvertFromString(variant==0?"#98D2C1":"#A9BECE"),70),null,new Rect(0,0,280,190));
            dc.DrawEllipse(Ink("#DDF4E8"),null,new Point(210,49),20,20);
            dc.DrawRoundedRectangle(Ink("#4BFFFFFF"),null,new Rect(30,95,165,80),30,30);
            dc.DrawRoundedRectangle(Ink("#473C7375"),null,new Rect(165,120,115,80),30,30);
        }
        string path=Path.Combine(fixtures,name);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(Render(visual,280,190)));
        using(var stream=File.Create(path))encoder.Save(stream);return path;
    }
    static BitmapSource Board(int chapter,double seconds,BitmapSource ui,VectorCatVisual cat){
        var visual=new DrawingVisual();
        using(var dc=visual.RenderOpen()){
            dc.DrawRectangle(new LinearGradientBrush(Ink("#102D34").Color,Ink("#2B5E57").Color,25),null,new Rect(0,0,1120,630));
            dc.DrawEllipse(Ink("#1223AB91"),null,new Point(1080,20),340,340);
            dc.DrawLine(new Pen(Ink("#355D62"),1),new Point(54,99),new Point(1066,99));
            Label(dc,Text("brand"),54,30,20,"#E6F6EF");Label(dc,Text("eyebrow"),775,34,13,"#A6C9C4");
            Label(dc,Words("chapterLabels")[chapter],56,149,13,"#8ED4C4");
            Label(dc,Words("titles")[chapter],54,194,34,"#F3FBF8");
            Label(dc,Words("bodies")[chapter],56,330,17,"#B4CECA");
            if(chapter<2){
                dc.DrawRoundedRectangle(Ink("#1520292F"),null,new Rect(418,174,650,354),16,16);
                dc.DrawImage(ui,new Rect(430,158,620,340));
                Label(dc,Words("captions")[chapter],434,524,12,"#91B3AD");
            }else{
                dc.DrawEllipse(Ink("#143A7F76"),new Pen(Ink("#376E70"),1),new Point(754,327),192,192);
                cat.SetPose(seconds>10.7?"sleep":"idle");cat.Animate(seconds);
                dc.DrawImage(CatFrame(cat),new Rect(654,202,200,224));
                Label(dc,Text("petCaption"),650,474,15,"#B4CECA");
                Label(dc,Words("captions")[chapter],650,513,12,"#91B3AD");
            }
            Label(dc,Text("footer"),54,581,11,"#81A6A1");
            for(int i=0;i<3;i++)dc.DrawRoundedRectangle(Ink(i==chapter?"#86D8C1":"#446B68"),null,new Rect(910+i*52,589,36,4),2,2);
        }
        return Render(visual,1120,630);
    }
    [STAThread] static int Main(string[] args){
        output=Path.GetFullPath(args[0]);fixtures=Path.Combine(output,"fixtures");Directory.CreateDirectory(fixtures);
        Directory.CreateDirectory(Path.Combine(output,"overview"));Directory.CreateDirectory(Path.Combine(output,"pet"));
        copy=Json.Deserialize<Dictionary<string,object>>(File.ReadAllText(args[1]));
        var app=new Application(){ShutdownMode=ShutdownMode.OnExplicitShutdown};
        var store=new Store(Path.Combine(fixtures,"state"));store.Data.UseEverything=false;store.Data.PetVisible=false;
        var documents=Words("documents").Select(Sample).ToArray();var pictures=Words("pictures").Select((name,index)=>Picture(name,index)).ToArray();
        var group=new Group(){Name=Text("documentTab"),Width=620,Height=340,LabelSize=15,IconSize=48};group.Items.AddRange(documents);
        var child=new Group(){Name=Text("pictureTab"),HostGroupId=group.Id};child.Items.AddRange(pictures);
        store.Data.Groups=new List<Group>{group,child};var controller=new Controller(store);
        var fence=new FenceWindow(controller,group){Left=-20000,Top=-20000,ShowActivated=false};
        var search=new QuickSearchView(controller);var searchHost=new Border(){Background=Ink("#F1F5F6"),CornerRadius=new CornerRadius(14),Child=search};
        var cat=new VectorCatVisual();cat.ApplySkin(Text("petSkin"));
        try{
            fence.Show();Pump();fence.UpdateLayout();Thumbnails.RequestVisible(fence);Pump();
            var docs=Render(fence,620,340);Save(docs,"partition-documents.png");
            group.ActiveGroupId=child.Id;fence.Refresh();Pump();Thumbnails.RequestVisible(fence);Pump();
            var photos=Render(fence,620,340);Save(photos,"partition-pictures.png");
            var searchStages=new List<BitmapSource>();
            foreach(string query in Words("queries")){
                search.Input.Text=query;
                if(query.Length>1){
                    var response=new SearchResponse(){Provider="Everything",Message=Text("searchMessage"),Items=new[]{
                        new SearchResult(){Path=documents[3],Changed=new DateTime(2026,10,17)},
                        new SearchResult(){Path=pictures[0],Changed=new DateTime(2026,10,17)}}};
                    typeof(QuickSearchView).GetMethod("Present",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(search,new object[]{response});
                }
                searchStages.Add(Snapshot(searchHost,620,340));Pump();
            }
            Save(searchStages.Last(),"search-all.png");
            for(int i=0;i<96;i++){
                int chapter=i/32;double seconds=i/8.0;
                BitmapSource ui=chapter==0?(i<16?docs:photos):searchStages[Math.Min(2,Math.Max(0,(i-32)/5))];
                Save(Board(chapter,seconds,ui,cat),Path.Combine("overview",i.ToString("D3")+".png"));
            }
            for(int i=0;i<80;i++){
                cat.SetPose("idle");cat.Animate(i*.08);
                var visual=new DrawingVisual();using(var dc=visual.RenderOpen()){
                    dc.DrawRoundedRectangle(Ink("#EDF4F2"),null,new Rect(0,0,240,264),22,22);
                    dc.DrawImage(CatFrame(cat),new Rect(40,24,160,180));
                    Label(dc,Text("brand"),46,225,13,"#37746B");
                }
                Save(Render(visual,240,264),Path.Combine("pet",i.ToString("D3")+".png"));
            }
            return 0;
        }finally{search.Dispose();fence.Close();app.Shutdown();}
    }
}
