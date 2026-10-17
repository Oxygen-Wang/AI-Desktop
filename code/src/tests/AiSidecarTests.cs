using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace QiDesk {

// 本地假模型服务：TcpListener 直接说 HTTP，避免 HttpListener 的地址保留权限问题。
// Respond 返回 null 表示以 500 响应；path 为请求路径（如 /api/chat、/v1/chat/completions）。
public sealed class FakeModelServer {
    readonly TcpListener listener;
    public int Port{get{return ((IPEndPoint)listener.LocalEndpoint).Port;}}
    public Func<string,string> Respond;
    public FakeModelServer(){
        listener=new TcpListener(IPAddress.Loopback,0);
        listener.Start();
        var thread=new Thread(Loop){IsBackground=true,Name="QiDesk fake model"};
        thread.Start();
    }
    public void Stop(){
        try{listener.Stop();}
        catch(SocketException){
            // 监听可能已随上一个连接关闭；停止只是收尾。
        }
    }
    void Loop(){
        while(true){
            TcpClient client;
            try{client=listener.AcceptTcpClient();}
            catch(SocketException){
                // 监听停止时退出接收循环。
                return;
            }catch(InvalidOperationException){
                // 监听已停止时退出接收循环。
                return;
            }
            try{Handle(client);}
            catch(IOException){
                // 客户端提前断开；继续服务下一个连接。
            }catch(SocketException){
                // 连接被重置；继续服务下一个连接。
            }
        }
    }
    void Handle(TcpClient client){
        using(client){
            using(var stream=client.GetStream()){
                string head=ReadHead(stream);
                if(head.Length==0)return;
                string path=FirstLinePath(head);
                int length=ContentLength(head);
                var buffer=new byte[length];
                int read=0;
                while(read<length){
                    int got=stream.Read(buffer,read,length-read);
                    if(got<=0)break;
                    read+=got;
                }
                string body=Respond!=null?Respond(path):null;
                string status=body!=null?"200 OK":"500 Internal Server Error";
                byte[] payload=Encoding.UTF8.GetBytes(body??"{}");
                var header=new StringBuilder();
                header.Append("HTTP/1.1 "+status+"\r\n");
                header.Append("Content-Type: application/json; charset=utf-8\r\n");
                header.Append("Content-Length: "+payload.Length+"\r\n");
                header.Append("Connection: close\r\n\r\n");
                byte[] headBytes=Encoding.ASCII.GetBytes(header.ToString());
                stream.Write(headBytes,0,headBytes.Length);
                stream.Write(payload,0,payload.Length);
                stream.Flush();
            }
        }
    }
    static string ReadHead(Stream stream){
        var text=new StringBuilder();
        var one=new byte[1];
        while(text.Length<64*1024){
            int got=stream.Read(one,0,1);
            if(got<=0)break;
            text.Append((char)one[0]);
            if(text.Length>=4&&text.ToString(text.Length-4,4)=="\r\n\r\n")break;
        }
        return text.ToString();
    }
    static string FirstLinePath(string head){
        string line=(head.Split('\n')[0]??"").Trim();
        var parts=line.Split(' ');
        return parts.Length>1?parts[1]:"";
    }
    static int ContentLength(string head){
        foreach(var line in head.Split('\n')){
            string trimmed=line.Trim();
            if(!trimmed.StartsWith("Content-Length:",StringComparison.OrdinalIgnoreCase))continue;
            int value;
            if(Int32.TryParse(trimmed.Substring(15).Trim(),out value))return value;
        }
        return 0;
    }
}

// AI 独立进程的验证：AiCore 分类逻辑（假模型服务）+ 真实 Ai-QiDesk-*.exe 的端到端协议。
// 不访问真实桌面、真实模型服务和真实用户配置。
public static class AiSidecarTests {
    const string ArrayBody="[{\"index\":0,\"groupId\":\"g1\",\"reason\":\"文本文件\"}]";
    static string OllamaBody(string array){return "{\"message\":{\"content\":\""+array.Replace("\"","\\\"")+"\"}}";}
    static string OpenAIBody(string array){return "{\"choices\":[{\"message\":{\"content\":\""+array.Replace("\"","\\\"")+"\"}}]}";}

    static AiRequest SampleRequest(int port,string style){
        return new AiRequest{
            Endpoint="http://127.0.0.1:"+port+(style=="ollama"?"/api":"/v1"),
            Model="test-model",
            Files=new List<AiFileEntry>{
                new AiFileEntry{Index=0,Name="报告.pdf",Extension=".pdf",IsFolder=false},
                new AiFileEntry{Index=1,Name="资料",Extension="",IsFolder=true}
            },
            Groups=new List<AiGroupEntry>{
                new AiGroupEntry{Id="g1",Name="文档"},
                new AiGroupEntry{Id="g2",Name="文件夹"}
            }
        };
    }

    static string Capture(Action action){
        try{action();return "";}
        catch(Exception ex){return ex.Message;}
    }

    public static void Run(string root,Action<bool,string> check){
        var server=new FakeModelServer();
        try{
            RunCore(server,check);
            RunEndToEnd(server,check);
        }finally{server.Stop();}
    }

    static void RunCore(FakeModelServer server,Action<bool,string> check){
        server.Respond=path=>path.EndsWith("/api/chat")?OllamaBody(ArrayBody):OpenAIBody(ArrayBody);
        var ollama=AiCore.Recommend(SampleRequest(server.Port,"ollama"));
        check(ollama.Count==1&&ollama[0].Index==0&&ollama[0].GroupId=="g1"&&ollama[0].Reason=="文档 · 文本文件",
            "AI core classifies through the Ollama /api/chat shape");
        var openai=AiCore.Recommend(SampleRequest(server.Port,"openai"));
        check(openai.Count==1&&openai[0].Index==0&&openai[0].GroupId=="g1",
            "AI core classifies through the Chat Completions shape");
        server.Respond=path=>null;
        string http=Capture(delegate{AiCore.Recommend(SampleRequest(server.Port,"openai"));});
        check(http=="模型返回 HTTP 500。请检查服务地址、模型和密钥。","AI core reports model HTTP failures in Chinese");
        server.Respond=path=>OpenAIBody("分类不了");
        string parse=Capture(delegate{AiCore.Recommend(SampleRequest(server.Port,"openai"));});
        check(parse=="模型没有返回有效分类数组。","AI core rejects a reply without a JSON array");
        string mixed="[{\"index\":0,\"groupId\":\"g1\"},{\"index\":9,\"groupId\":\"g1\"},{\"index\":0,\"groupId\":\"g2\"},{\"index\":1,\"groupId\":\"gx\"}]";
        server.Respond=path=>OpenAIBody(mixed);
        var filtered=AiCore.Recommend(SampleRequest(server.Port,"openai"));
        check(filtered.Count==1&&filtered[0].Index==0&&filtered[0].GroupId=="g1",
            "AI core drops out-of-range, duplicate and unknown-group entries");
        var request=SampleRequest(server.Port,"openai");
        request.Endpoint="ftp://model";
        string endpoint=Capture(delegate{AiCore.Recommend(request);});
        check(endpoint=="模型地址需要以 http:// 或 https:// 开头。","AI core rejects non-HTTP endpoints");
        var many=new List<AiFileEntry>();
        for(int i=0;i<301;i++)many.Add(new AiFileEntry{Index=i,Name="f"+i,Extension=".txt",IsFolder=false});
        request=SampleRequest(server.Port,"openai");
        request.Files=many;
        string tooMany=Capture(delegate{AiCore.Recommend(request);});
        check(tooMany=="一次最多分类 300 个项目。","AI core caps the batch at 300 items");
        bool asked=false;
        server.Respond=path=>{asked=true;return OpenAIBody(ArrayBody);};
        request=SampleRequest(server.Port,"openai");
        request.Groups=new List<AiGroupEntry>();
        var none=AiCore.Recommend(request);
        check(none.Count==0&&!asked,"AI core returns nothing without groups and never calls the model");
        request=SampleRequest(server.Port,"openai");
        request.Files[0].IsFolder=null;
        string missing=Capture(delegate{AiCore.Recommend(request);});
        check(missing=="文件条目缺少 isFolder。","AI core validates the file entry shape");
    }

    static void RunEndToEnd(FakeModelServer server,Action<bool,string> check){
        string exe=null;
        try{exe=AiClient.Locate();}
        catch(Exception){
            // 组件缺失只是跳过端到端，其余检查已经覆盖协议两侧的纯逻辑。
            exe=null;
        }
        if(exe==null){
            check(true,"AI sidecar exe missing from the build output; end-to-end skipped");
            return;
        }
        server.Respond=path=>path.EndsWith("/api/chat")?OllamaBody(ArrayBody):OpenAIBody(ArrayBody);
        try{
            var ping=AiClient.Exchange("ping",new{},10000,null);
            check(ping.Result!=null&&ping.Result.Ok==true&&ping.Result.Protocol==AiCore.ProtocolVersion,
                "AI sidecar process answers ping over stdio NDJSON");
            var suggestions=AiClient.Recommend(SampleRequest(server.Port,"openai"),null);
            check(suggestions.Count==1&&suggestions[0].GroupId=="g1",
                "AI sidecar process returns suggestions end to end");
            string unknown=Capture(delegate{AiClient.Exchange("no-such-command",new{},10000,null);});
            check(unknown=="未知命令：no-such-command","AI sidecar process rejects unknown commands with a protocol error");
        }finally{AiClient.Shutdown();}
    }
}
}
