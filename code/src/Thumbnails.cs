using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Interop;

namespace QiDesk {
public class ThumbnailImage : Image {
    public static readonly DependencyProperty FilePathProperty=DependencyProperty.Register("FilePath",typeof(string),typeof(ThumbnailImage),new PropertyMetadata("",Changed));
    public string FilePath {get{return (string)GetValue(FilePathProperty);}set{SetValue(FilePathProperty,value);}}
    static void Changed(DependencyObject source,DependencyPropertyChangedEventArgs e){var image=(ThumbnailImage)source;Thumbnails.Bind(image,Convert.ToString(e.NewValue),Math.Max(32,image.Width));}
}
public static class Thumbnails {
    [ComImport,Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IImageFactory {
        [PreserveSig]int GetImage(SizeNative size,uint flags,out IntPtr bitmap);
    }
    [StructLayout(LayoutKind.Sequential)] struct SizeNative {public int Width,Height;public SizeNative(int n){Width=n;Height=n;}}
    [DllImport("shell32.dll",CharSet=CharSet.Unicode)] static extern int SHCreateItemFromParsingName(string path,IntPtr context,ref Guid iid,[MarshalAs(UnmanagedType.Interface)]out IImageFactory item);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr bitmap);
    sealed class Binding {public string Path,Key;public int Size;public bool Pending;}
    sealed class Entry {public ImageSource Image;public long Bytes;public DateTime Stamp;}
    sealed class Job {public string Path,Key;public int Size;}
    static readonly object gate=new object();
    static readonly Dictionary<string,Entry> cache=new Dictionary<string,Entry>(StringComparer.OrdinalIgnoreCase);
    static readonly Dictionary<string,List<WeakReference>> waiting=new Dictionary<string,List<WeakReference>>(StringComparer.OrdinalIgnoreCase);
    static readonly BlockingCollection<Job> queue=new BlockingCollection<Job>(512);
    static long bytes;static int active;static bool started;
    static readonly DependencyProperty BindingProperty=DependencyProperty.RegisterAttached("ThumbnailBinding",typeof(Binding),typeof(Thumbnails),new PropertyMetadata(null));
    public static void UseStatic(Image image,ImageSource source){
        // Clear the old request identity so an in-flight Shell thumbnail cannot
        // overwrite an explicitly chosen theme icon after the view changes.
        image.SetValue(BindingProperty,null);image.Loaded-=Loaded;image.Source=source;
    }
    public static string Revision(string path){try{var file=new FileInfo(path);return file.LastWriteTimeUtc.Ticks+":"+(file.Exists?file.Length:0);}catch{return "unavailable";}}
    public static bool IsPicture(string path){return new[]{".png",".jpg",".jpeg",".bmp",".gif",".tif",".tiff",".webp",".heic",".avif",".ico"}.Contains(Path.GetExtension(path).ToLowerInvariant());}
    public static string Key(string path,int size){return Path.GetFullPath(path)+"|"+size+"|"+Revision(path);}
    public static void Bind(Image image,string path,double logicalSize){
        if(String.IsNullOrEmpty(path)){image.Source=null;image.SetValue(BindingProperty,null);return;}
        var source=PresentationSource.FromVisual(image);double scale=source==null?1:source.CompositionTarget.TransformToDevice.M11;int requested=(int)Math.Ceiling(logicalSize*scale);int size=requested<=48?48:requested<=96?96:requested<=160?160:requested<=256?256:320;
        image.Source=Shell.Icon(path);RenderOptions.SetBitmapScalingMode(image,BitmapScalingMode.HighQuality);image.Stretch=Stretch.Uniform;
        image.SetValue(BindingProperty,new Binding(){Path=path,Key=Key(path,size),Size=size});
        image.Loaded-=Loaded;image.Loaded+=Loaded;
        if(image.IsLoaded)Request(image);
    }
    static void Loaded(object sender,RoutedEventArgs e){Request((Image)sender);}
    public static void RequestVisible(DependencyObject root){if(root==null)return;var image=root as Image;if(image!=null)Request(image);for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)RequestVisible(VisualTreeHelper.GetChild(root,i));}
    static bool InViewport(Image image){if(!image.IsVisible||image.ActualWidth<=0||image.ActualHeight<=0)return false;DependencyObject part=image;while((part=VisualTreeHelper.GetParent(part))!=null){var scroll=part as ScrollViewer;if(scroll==null)continue;try{Rect rect=image.TransformToAncestor(scroll).TransformBounds(new Rect(0,0,image.ActualWidth,image.ActualHeight));return rect.IntersectsWith(new Rect(0,0,scroll.ActualWidth,scroll.ActualHeight));}catch{return false;}}return true;}
    static void Request(Image image){
        var binding=image.GetValue(BindingProperty) as Binding;if(binding==null||binding.Pending||!InViewport(image))return;
        lock(gate){Entry entry;if(cache.TryGetValue(binding.Key,out entry)){if(entry.Image==null&&DateTime.UtcNow-entry.Stamp>TimeSpan.FromSeconds(20)){cache.Remove(binding.Key);}else{if(entry.Image!=null){entry.Stamp=DateTime.UtcNow;image.Source=entry.Image;}return;}}List<WeakReference> targets;if(waiting.TryGetValue(binding.Key,out targets)){targets.Add(new WeakReference(image));binding.Pending=true;return;}targets=new List<WeakReference>{new WeakReference(image)};waiting[binding.Key]=targets;binding.Pending=true;if(!queue.TryAdd(new Job(){Key=binding.Key,Path=binding.Path,Size=binding.Size})){binding.Pending=false;waiting.Remove(binding.Key);return;}if(!started){started=true;var thread=new Thread(Worker){IsBackground=true,Name="QiDesk thumbnails"};thread.SetApartmentState(ApartmentState.STA);thread.Start();}}
    }
    static void Worker(){foreach(var job in queue.GetConsumingEnumerable()){Interlocked.Increment(ref active);ImageSource image=null;try{image=Extract(job.Path,job.Size);}catch{}List<WeakReference> targets;lock(gate){long cost=image==null?0:(long)((BitmapSource)image).PixelWidth*((BitmapSource)image).PixelHeight*4;cache[job.Key]=new Entry(){Image=image,Bytes=cost,Stamp=DateTime.UtcNow};bytes+=cost;while(bytes>32*1024*1024||cache.Count>512){string oldest=cache.OrderBy(x=>x.Value.Stamp).First().Key;bytes-=cache[oldest].Bytes;cache.Remove(oldest);}targets=waiting[job.Key];waiting.Remove(job.Key);}foreach(var reference in targets){var target=reference.Target as Image;if(target==null||target.Dispatcher.HasShutdownStarted)continue;try{target.Dispatcher.BeginInvoke(new Action(delegate{var current=target.GetValue(BindingProperty) as Binding;if(current==null||current.Key!=job.Key)return;current.Pending=false;if(image!=null)target.Source=image;}),DispatcherPriority.Background);}catch{}}Interlocked.Decrement(ref active);}}
    public static BitmapSource Extract(string path,int size){
        // Decode common raster images directly to preserve PNG transparency and avoid
        // stale / incorrectly premultiplied Shell thumbnails at larger resolutions.
        string ext=Path.GetExtension(path).ToLowerInvariant();bool raster=new[]{".png",".jpg",".jpeg",".bmp",".gif",".tif",".tiff",".ico"}.Contains(ext);
        if(raster){var decoded=Decode(path,size);if(decoded!=null)return decoded;}
        IImageFactory factory=null;IntPtr bitmap=IntPtr.Zero;
        try{Guid iid=typeof(IImageFactory).GUID;if(SHCreateItemFromParsingName(path,IntPtr.Zero,ref iid,out factory)>=0&&factory.GetImage(new SizeNative(Math.Max(16,Math.Min(512,size))),8|1,out bitmap)>=0&&bitmap!=IntPtr.Zero){var source=Imaging.CreateBitmapSourceFromHBitmap(bitmap,IntPtr.Zero,Int32Rect.Empty,BitmapSizeOptions.FromEmptyOptions());source.Freeze();return source;}}
        catch{}finally{if(bitmap!=IntPtr.Zero)DeleteObject(bitmap);if(factory!=null)Marshal.ReleaseComObject(factory);}
        return raster?Decode(path,size):null;
    }
    static BitmapSource Decode(string path,int size){
        try{using(var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)){var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.DecodePixelWidth=Math.Max(16,Math.Min(512,size));image.StreamSource=stream;image.EndInit();image.Freeze();return image;}}catch{return null;}
    }
    public static void Drain(int milliseconds){var watch=System.Diagnostics.Stopwatch.StartNew();do{var frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(delegate{frame.Continue=false;}));Dispatcher.PushFrame(frame);if(queue.Count==0&&Interlocked.CompareExchange(ref active,0,0)==0){var last=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(delegate{last.Continue=false;}));Dispatcher.PushFrame(last);return;}Thread.Sleep(10);}while(watch.ElapsedMilliseconds<milliseconds);}
}
}
