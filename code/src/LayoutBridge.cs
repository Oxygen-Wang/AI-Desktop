using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Threading;

namespace QiDesk {
// 运行中的栖桌实例提供一个本地命令通道，供 CLI 和后续 MCP 工具共用。
// 命令只作用于布局档案；真实文件仍由现有文件操作负责。
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
        return Dispatch(controller,command,args??new Dictionary<string,object>());
    }
    // CLI 与 MCP 共用这条分发路径：参数校验在 ValidateCall，命令实现在 LayoutCommands，
    // 桥接层只包一层管道格式，不再各写一套分支。
    internal static object Dispatch(Controller owner,string command,Dictionary<string,object> args){
        args=args??new Dictionary<string,object>();
        McpBridge.ValidateCall(new Dictionary<string,object>{{"name",command},{"arguments",args}});
        return new {ok=true,result=LayoutCommands.Execute(owner,command,args)};
    }
    static string ReadLine(StreamReader reader){
        var text=new StringBuilder();int c;
        while((c=reader.Read())>=0){if(c=='\n')return text.ToString();if(c!='\r')text.Append((char)c);if(text.Length>2*1024*1024)throw new InvalidDataException("请求过大");}
        return text.Length==0?null:text.ToString();
    }
    static string Text(Dictionary<string,object> map,string key){object value;return map!=null&&map.TryGetValue(key,out value)&&value!=null?Convert.ToString(value):"";}
}
}
