using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace QiDesk {
// 运行中的栖桌实例提供一个本地命令通道，CLI 经此调用全部 MCP 工具。
// 校验与实现都在 McpBridge.Execute，这里只包一层管道格式。
public static class LayoutBridge {
    public static string PipeName{get{return "QiDesk-Layout-"+WindowsIdentity.GetCurrent().User.Value;}}
    static Controller controller;
    static volatile bool started;
    public static void Start(Controller owner){
        if(started||owner==null||owner.Manager==null)return;
        started=true;controller=owner;
        var thread=new Thread(Listen){IsBackground=true,Name="QiDesk layout bridge"};
        thread.Start();
    }
    static void Listen(){
        while(true){
            try{
                var security=new PipeSecurity();security.SetAccessRuleProtection(true,false);
                security.AddAccessRule(new PipeAccessRule(WindowsIdentity.GetCurrent().User,PipeAccessRights.FullControl,AccessControlType.Allow));
                using(var pipe=new NamedPipeServerStream(PipeName,PipeDirection.InOut,1,PipeTransmissionMode.Byte,PipeOptions.None,1<<20,1<<20,security)){
                    pipe.WaitForConnection();
                    using(var reader=new StreamReader(pipe,new UTF8Encoding(false),false,4096,true))
                    using(var writer=new StreamWriter(pipe,new UTF8Encoding(false),4096,true){AutoFlush=true}){
                        string line=ReadLine(reader);
                        if(line==null)continue;
                        string response;
                        try{
                            var request=Store.Json.Deserialize<Dictionary<string,object>>(line);
                            object result=controller.Manager.Dispatcher.Invoke(new Func<object>(delegate{return Execute(request);}));
                            response=Store.Json.Serialize(result);
                        }catch(Exception ex){response=Store.Json.Serialize(new {error=ex.Message});}
                        writer.WriteLine(response);
                    }
                }
            }catch(Exception ex){
                try{File.AppendAllText(Path.Combine(controller.Store.Root,"layout-bridge-errors.log"),DateTime.Now+" "+ex.Message+Environment.NewLine);}catch{}
                Thread.Sleep(400);
            }
        }
    }
    static object Execute(Dictionary<string,object> request){
        if(request==null)throw new InvalidDataException("请求为空");
        string command=Text(request,"name");
        var args=request.ContainsKey("arguments")?request["arguments"] as Dictionary<string,object>:null;
        // 文件搜索必须走异步通道：查询在后台线程完成，不占住 UI 线程，也不会死锁。
        if(command=="qidesk_search_files"){
            var done=new TaskCompletionSource<object>();
            controller.Manager.Dispatcher.BeginInvoke(new Action(async delegate{
                try{done.SetResult(new {ok=true,result=await McpBridge.ExecuteAsync(controller,request)});}
                catch(Exception ex){done.SetException(ex);}
            }));
            return done.Task.GetAwaiter().GetResult();
        }
        return controller.Manager.Dispatcher.Invoke(new Func<object>(delegate{return Dispatch(controller,command,args??new Dictionary<string,object>());}));
    }
    // CLI 与 MCP 共用这条分发路径：参数校验与全部工具的实现都在 McpBridge.Execute，
    // 桥接层只包一层管道格式，不再各写一套分支。
    internal static object Dispatch(Controller owner,string command,Dictionary<string,object> args){
        args=args??new Dictionary<string,object>();
        return new {ok=true,result=McpBridge.Execute(owner,new Dictionary<string,object>{{"name",command},{"arguments",args}})};
    }
    static string ReadLine(StreamReader reader){
        var text=new StringBuilder();int c;
        while((c=reader.Read())>=0){if(c=='\n')return text.ToString();if(c!='\r')text.Append((char)c);if(text.Length>2*1024*1024)throw new InvalidDataException("请求过大");}
        return text.Length==0?null:text.ToString();
    }
    static string Text(Dictionary<string,object> map,string key){object value;return map!=null&&map.TryGetValue(key,out value)&&value!=null?Convert.ToString(value):"";}
}
}
