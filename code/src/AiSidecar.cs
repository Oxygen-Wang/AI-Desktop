using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;

namespace QiDesk {

// 协议信封类型。主程序与 AI 进程两侧共用；字段名与 JSON 大小写不敏感匹配。
public class AiResult {
    public List<AiSuggestion> Suggestions {get;set;}
    public bool? Ok {get;set;}
    public string Version {get;set;}
    public string Protocol {get;set;}
    public bool? Shutdown {get;set;}
}

public class AiErrorInfo {
    public string Code {get;set;}
    public string Message {get;set;}
}

public class AiMessage {
    public string Type {get;set;}
    public string Id {get;set;}
    public string Command {get;set;}
    public string Event {get;set;}
    public bool? Ok {get;set;}
    public AiResult Result {get;set;}
    public AiErrorInfo Error {get;set;}
    public Dictionary<string,object> Data {get;set;}
}

public class AiRequestEnvelope {
    public string Type {get;set;}
    public string Id {get;set;}
    public string Command {get;set;}
    public AiRequest Params {get;set;}
}

// AI 功能独立进程（Ai-QiDesk-*.exe --ai-serve）的服务端：stdin/stdout 上的 NDJSON 循环。
// 只调用 AiCore 的纯数据接口，不接触 Store 和窗口；进程管理与复用在主程序的 AiClient。
// 协议约定见 research/AI-SIDECAR.md。
public static class AiSidecar {
    public const string Version="0.7.1";
    static readonly TimeSpan IdleExit=TimeSpan.FromMinutes(10);
    const int MaxLineBytes=2*1024*1024;

    public static int Serve(){
        using(var input=new StreamReader(Console.OpenStandardInput(),new UTF8Encoding(false),false,4096))
        using(var output=new StreamWriter(Console.OpenStandardOutput(),new UTF8Encoding(false)){AutoFlush=true}){
            var idle=new Timer(delegate{Environment.Exit(0);},null,IdleExit,IdleExit);
            try{
                WriteLine(output,Event("ready",new{version=Version,protocol=AiCore.ProtocolVersion}));
                string line;
                while((line=ReadLine(input))!=null){
                    idle.Change(IdleExit,IdleExit);
                    bool shutdown=Handle(line,output);
                    if(shutdown)return 0;
                }
            }finally{idle.Dispose();}
        }
        return 0;
    }

    // 返回 true 表示收到 shutdown，主循环应退出。
    static bool Handle(string line,StreamWriter output){
        string id="unknown";
        try{
            AiRequestEnvelope request=Store.Json.Deserialize<AiRequestEnvelope>(line);
            if(request==null)throw new AiProtocolException("BAD_REQUEST","请求不是合法的 JSON。");
            id=request.Id??"unknown";
            if(request.Type!="request"||String.IsNullOrEmpty(request.Id)||String.IsNullOrEmpty(request.Command))
                throw new AiProtocolException("BAD_REQUEST","请求缺少 type、id 或 command。");
            if(request.Command=="shutdown"){
                WriteLine(output,Response(id,true,new AiResult{Shutdown=true},null));
                return true;
            }
            if(request.Command=="ping"){
                WriteLine(output,Response(id,true,new AiResult{Ok=true,Version=Version,Protocol=AiCore.ProtocolVersion},null));
                return false;
            }
            if(request.Command=="recommend"){
                WriteLine(output,Event("progress",new{message="正在请求模型…"}));
                var result=AiCore.Recommend(request.Params??new AiRequest());
                WriteLine(output,Response(id,true,new AiResult{Suggestions=result},null));
                return false;
            }
            throw new AiProtocolException("BAD_REQUEST","未知命令："+request.Command);
        }catch(AiProtocolException ex){
            WriteLine(output,Event("error",new{code=ex.Code,message=ex.Message}));
            WriteLine(output,Response(id,false,null,new AiErrorInfo{Code=ex.Code,Message=ex.Message}));
            return false;
        }catch(Exception ex){
            WriteLine(output,Response(id,false,null,new AiErrorInfo{Code="INTERNAL_ERROR",Message=ex.Message}));
            return false;
        }
    }

    public static string Request(string id,string command,object parameters){
        return Store.Json.Serialize(new{type="request",id=id,command=command,@params=parameters});
    }

    public static string Response(string id,bool ok,AiResult result,AiErrorInfo error){
        if(ok)return Store.Json.Serialize(new{type="response",id=id,ok=true,result=result});
        return Store.Json.Serialize(new{type="response",id=id,ok=false,error=error});
    }

    public static string Event(string name,object data){
        return Store.Json.Serialize(new{type="event",@event=name,data=data});
    }

    static void WriteLine(StreamWriter output,string line){
        output.Write(line+"\n");
        output.Flush();
    }

    // 与 LayoutBridge / McpSession 一致的有界行读取：EOF 返回 null，超限抛错。
    static string ReadLine(StreamReader reader){
        var text=new StringBuilder();
        int c;
        while((c=reader.Read())>=0){
            if(c=='\n')return text.ToString();
            if(c!='\r')text.Append((char)c);
            if(text.Length>MaxLineBytes)throw new InvalidDataException("AI 组件输入行过大。");
        }
        return text.Length==0?null:text.ToString();
    }
}
}
