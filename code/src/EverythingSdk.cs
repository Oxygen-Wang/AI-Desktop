using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;

namespace QiDesk {
// Everything's SDK has process-global query state. One message window serializes
// callers, and reply IDs prevent a cancelled query from overwriting newer results.
public static class EverythingSdk {
    static readonly SemaphoreSlim gate=new SemaphoreSlim(1,1);
    static Worker worker;
    static IntPtr library;
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr LoadLibraryEx(string file,IntPtr reserved,uint flags);
    [DllImport("Everything64.dll",CharSet=CharSet.Unicode,ExactSpelling=true)] static extern void Everything_SetSearchW(string text);
    [DllImport("Everything64.dll")] static extern void Everything_SetOffset(uint offset);
    [DllImport("Everything64.dll")] static extern void Everything_SetMax(uint max);
    [DllImport("Everything64.dll")] static extern void Everything_SetRequestFlags(uint flags);
    [DllImport("Everything64.dll")] static extern void Everything_SetRegex([MarshalAs(UnmanagedType.Bool)] bool regex);
    [DllImport("Everything64.dll")] static extern void Everything_SetMatchPath([MarshalAs(UnmanagedType.Bool)] bool value);
    [DllImport("Everything64.dll")] static extern void Everything_SetMatchCase([MarshalAs(UnmanagedType.Bool)] bool value);
    [DllImport("Everything64.dll")] static extern void Everything_SetReplyWindow(IntPtr hwnd);
    [DllImport("Everything64.dll")] static extern void Everything_SetReplyID(uint id);
    [DllImport("Everything64.dll")] static extern void Everything_SetSort(uint sort);
    [DllImport("Everything64.dll",ExactSpelling=true)] [return:MarshalAs(UnmanagedType.Bool)] static extern bool Everything_QueryW([MarshalAs(UnmanagedType.Bool)] bool wait);
    [DllImport("Everything64.dll")] [return:MarshalAs(UnmanagedType.Bool)] static extern bool Everything_IsQueryReply(uint msg,IntPtr w,IntPtr l,uint id);
    [DllImport("Everything64.dll")] static extern uint Everything_GetNumResults();
    [DllImport("Everything64.dll",CharSet=CharSet.Unicode,ExactSpelling=true)] static extern uint Everything_GetResultFullPathNameW(uint index,StringBuilder path,uint length);
    [DllImport("Everything64.dll")] [return:MarshalAs(UnmanagedType.Bool)] static extern bool Everything_GetResultDateModified(uint index,out long time);
    [DllImport("Everything64.dll")] [return:MarshalAs(UnmanagedType.Bool)] static extern bool Everything_GetResultSize(uint index,out long bytes);

    static bool Load(State state){
        if(library!=IntPtr.Zero)return true;
        if(IntPtr.Size!=8)return false;
        string[] candidates=EverythingRuntime.Candidates(AppDomain.CurrentDomain.BaseDirectory,
            state.EverythingPath,"Everything64.dll");
        foreach(string path in candidates.Where(File.Exists)){
            library=LoadLibraryEx(path,IntPtr.Zero,0x8); // absolute, altered DLL search path
            if(library!=IntPtr.Zero)return true;
        }
        return false;
    }
    public static SearchResult[] Query(State state,string expression,bool modified,int limit,CancellationToken token){
        gate.Wait(token);
        try{
            if(!Load(state))throw new DllNotFoundException("未找到可选 Everything SDK。");
            if(worker==null)worker=new Worker();
            var request=worker.Start(expression,modified,limit);
            try{
                if(!request.Task.Wait(3500,token))throw new TimeoutException("Everything 查询超时。");
                return request.Task.GetAwaiter().GetResult();
            }catch(AggregateException ex){throw ex.InnerException;}
            finally{worker.Cancel(request);}
        }finally{gate.Release();}
    }
    sealed class Worker {
        Dispatcher dispatcher;
        HwndSource window;
        TaskCompletionSource<SearchResult[]> active;
        uint id;
        public Worker(){
            var ready=new ManualResetEventSlim();Exception failure=null;
            var thread=new Thread(delegate(){
                try{
                    dispatcher=Dispatcher.CurrentDispatcher;
                    var options=new HwndSourceParameters("QiDesk Everything reply"){ParentWindow=new IntPtr(-3),WindowStyle=0};
                    window=new HwndSource(options);window.AddHook(Reply);
                }catch(Exception ex){failure=ex;}
                finally{ready.Set();}
                if(failure==null)Dispatcher.Run();
            }){IsBackground=true,Name="Everything query replies"};
            thread.SetApartmentState(ApartmentState.STA);thread.Start();
            ready.Wait();ready.Dispose();if(failure!=null)throw failure;
        }
        public TaskCompletionSource<SearchResult[]> Start(string text,bool modified,int max){
            var result=new TaskCompletionSource<SearchResult[]>();
            dispatcher.BeginInvoke(new Action(delegate{
                active=result;id++;if(id==0)id++;
                try{
                    Everything_SetSearchW(text);Everything_SetOffset(0);Everything_SetMax((uint)max);
                    Everything_SetRequestFlags(1|2|0x10|0x40);Everything_SetRegex(false);
                    Everything_SetMatchPath(false);Everything_SetMatchCase(false);
                    Everything_SetReplyWindow(window.Handle);Everything_SetReplyID(id);Everything_SetSort(modified?14u:1u);
                    if(!Everything_QueryW(false))result.TrySetException(new IOException("Everything 未连接，请先启动 Everything。"));
                }catch(Exception ex){result.TrySetException(ex);}
            }));
            return result;
        }
        public void Cancel(TaskCompletionSource<SearchResult[]> request){dispatcher.BeginInvoke(new Action(delegate{if(active==request)active=null;}));}
        IntPtr Reply(IntPtr hwnd,int msg,IntPtr w,IntPtr l,ref bool handled){
            if(msg!=0x4A||active==null)return IntPtr.Zero;
            try{
                if(!Everything_IsQueryReply((uint)msg,w,l,id))return IntPtr.Zero;
                handled=true;uint count=Math.Min(Everything_GetNumResults(),300u);
                var result=new SearchResult[count];
                for(uint i=0;i<count;i++){
                    var path=new StringBuilder(32768);Everything_GetResultFullPathNameW(i,path,(uint)path.Capacity);
                    long bytes,time;Everything_GetResultSize(i,out bytes);
                    DateTime changed=Everything_GetResultDateModified(i,out time)&&time>0?DateTime.FromFileTime(time):DateTime.MinValue;
                    result[i]=new SearchResult(){Path=path.ToString(),Bytes=Math.Max(0,bytes),Changed=changed};
                }
                active.TrySetResult(result);
            }catch(Exception ex){active.TrySetException(ex);}
            return new IntPtr(1);
        }
    }
}
}
