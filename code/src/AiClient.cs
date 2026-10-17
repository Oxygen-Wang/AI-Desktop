using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QiDesk {

// 主程序侧的 AI 组件客户端：发现 Ai-QiDesk-*.exe、按 NDJSON 协议收发、管理进程生命周期。
// 这里不知道提示词、HTTP 和解析规则——那些都在 AI 进程里的 AiCore。
// 协议约定见 research/AI-SIDECAR.md。
public static class AiClient {
    static readonly object Gate=new object();
    const int ExchangeTimeoutMs=100000;
    const int ReadyTimeoutMs=10000;
    const int MaxLineChars=2*1024*1024;
    static Process child;
    static StreamReader input;
    static Stream output;
    static int sequence;

    // 应用入口：枚举桌面项、按忽略规则过滤，发给 AI 进程后再映射回真实路径。
    // 与迁移前 Ai.Recommend 的行为一致：最多 300 项、只发文件名/扩展名/是否文件夹和分组名。
    public static List<Suggestion> Recommend(Store store,Action<string> progress){
        var paths=Store.DesktopItems().Where(p=>!store.Ignored(p,null)).Take(AiCore.MaxFiles).ToList();
        var groups=store.Data.Groups.Where(g=>String.IsNullOrEmpty(g.Folder)).ToList();
        var request=new AiRequest{
            Endpoint=store.Data.AiEndpoint??"",
            Model=store.Data.AiModel??"",
            Files=paths.Select((p,i)=>new AiFileEntry{Index=i,Name=Path.GetFileName(p),Extension=Path.GetExtension(p),IsFolder=Directory.Exists(p)}).ToList(),
            Groups=groups.Select(g=>new AiGroupEntry{Id=g.Id,Name=g.Name}).ToList()
        };
        var accepted=Recommend(request,progress);
        var result=new List<Suggestion>();
        foreach(var item in accepted){
            if(item.Index<0||item.Index>=paths.Count)continue;
            string path=paths[item.Index];
            Group target=groups.FirstOrDefault(g=>g.Id==item.GroupId);
            if(target==null||store.Ignored(path,target))continue;
            result.Add(new Suggestion(){Path=path,GroupId=item.GroupId,Reason=item.Reason});
        }
        return result;
    }

    internal static List<AiSuggestion> Recommend(AiRequest request,Action<string> progress){
        var message=Exchange("recommend",request,ExchangeTimeoutMs,progress);
        var result=message.Result;
        if(result==null||result.Suggestions==null)return new List<AiSuggestion>();
        return result.Suggestions;
    }

    // 退出栖桌时调用：先礼貌 shutdown，再 Kill 兜底。请求进行中也不会死等。
    public static void Shutdown(){
        Process target;
        lock(Gate){
            target=child;
            child=null;input=null;output=null;
        }
        if(target==null)return;
        try{
            if(!target.HasExited){
                byte[] bytes=new UTF8Encoding(false).GetBytes(AiSidecar.Request("quit","shutdown",new{})+"\n");
                target.StandardInput.BaseStream.Write(bytes,0,bytes.Length);
                target.StandardInput.BaseStream.Flush();
                target.WaitForExit(2000);
            }
        }catch(IOException){
            // 收尾阶段管道可能已随进程关闭，下面统一 Kill。
        }
        try{
            if(!target.HasExited)target.Kill();
        }catch(InvalidOperationException){
            // 进程在两次判断之间退出。
        }catch(System.ComponentModel.Win32Exception){
            // 拒绝终止时进程通常也在退出中。
        }
        try{target.Dispose();}
        catch(Exception){
            // 退出收尾：释放句柄失败也不阻塞退出。
        }
    }

    internal static AiMessage Exchange(string command,object parameters,int timeoutMs,Action<string> onProgress){
        lock(Gate){
            EnsureStarted();
            string id=(++sequence).ToString();
            WriteLine(AiSidecar.Request(id,command,parameters));
            var deadline=DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while(true){
                int remaining=(int)(deadline-DateTime.UtcNow).TotalMilliseconds;
                if(remaining<=0)Fail("模型请求超时。");
                string line;
                if(!TryReadLine(remaining,out line))Fail("模型请求超时。");
                if(line==null)Fail("AI 组件异常退出。");
                AiMessage message=null;
                try{message=Store.Json.Deserialize<AiMessage>(line);}catch{message=null;}
                if(message==null)continue;
                if(message.Type=="event"){
                    if(message.Event=="progress"&&onProgress!=null&&message.Data!=null){
                        object text;
                        if(message.Data.TryGetValue("message",out text)&&text!=null)onProgress(Convert.ToString(text));
                    }
                    continue;
                }
                if(message.Type!="response"||message.Id!=id)continue;
                if(message.Ok==true)return message;
                string error=message.Error!=null&&!String.IsNullOrEmpty(message.Error.Message)
                    ?message.Error.Message:"AI 组件返回了未知错误。";
                throw new Exception(error);
            }
        }
    }

    static void EnsureStarted(){
        if(child!=null&&!child.HasExited)return;
        KillChild();
        string exe=Locate();
        var info=new ProcessStartInfo(exe,"--ai-serve"){
            UseShellExecute=false,
            RedirectStandardInput=true,
            RedirectStandardOutput=true,
            RedirectStandardError=true,
            CreateNoWindow=true,
            WorkingDirectory=AppDomain.CurrentDomain.BaseDirectory
        };
        var started=new Process(){StartInfo=info};
        try{started.Start();}
        catch(Exception ex){throw new Exception("无法启动 AI 组件（"+Path.GetFileName(exe)+"）："+ex.Message);}
        child=started;
        output=started.StandardInput.BaseStream;
        input=new StreamReader(started.StandardOutput.BaseStream,new UTF8Encoding(false),false,4096);
        started.ErrorDataReceived+=delegate{};
        started.BeginErrorReadLine();
        WaitReady();
    }

    static void WaitReady(){
        var deadline=DateTime.UtcNow.AddMilliseconds(ReadyTimeoutMs);
        while(true){
            int remaining=(int)(deadline-DateTime.UtcNow).TotalMilliseconds;
            if(remaining<=0)Fail("AI 组件未能在 10 秒内就绪。");
            string line;
            if(!TryReadLine(remaining,out line))Fail("AI 组件未能在 10 秒内就绪。");
            if(line==null)Fail("AI 组件启动后立即退出。");
            AiMessage message=null;
            try{message=Store.Json.Deserialize<AiMessage>(line);}catch{message=null;}
            if(message!=null&&message.Type=="event"&&message.Event=="ready")return;
        }
    }

    static void WriteLine(string line){
        var stream=output;
        if(stream==null)throw new Exception("AI 组件未运行。");
        byte[] bytes=new UTF8Encoding(false).GetBytes(line+"\n");
        stream.Write(bytes,0,bytes.Length);
        stream.Flush();
    }

    // false 表示超时；line 为 null 表示 EOF。读取线程里的异常统一按组件故障处理。
    static bool TryReadLine(int timeoutMs,out string line){
        line=null;
        var reader=input;
        if(reader==null)return true;
        var pending=Task.Run(()=>ReadRaw(reader));
        if(!pending.Wait(timeoutMs))return false;
        try{line=pending.Result;}
        catch(AggregateException){Fail("AI 组件返回内容过大或读取失败。");}
        return true;
    }

    static string ReadRaw(StreamReader reader){
        var text=new StringBuilder();
        int c;
        while((c=reader.Read())>=0){
            if(c=='\n')return text.ToString();
            if(c!='\r')text.Append((char)c);
            if(text.Length>MaxLineChars)throw new InvalidDataException("AI 组件返回行过大。");
        }
        return text.Length==0?null:text.ToString();
    }

    static void Fail(string message){
        KillChild();
        throw new Exception(message);
    }

    static void KillChild(){
        var target=child;
        child=null;input=null;output=null;
        if(target==null)return;
        try{
            if(!target.HasExited)target.Kill();
        }catch(InvalidOperationException){
            // 进程在两次判断之间退出。
        }catch(System.ComponentModel.Win32Exception){
            // 拒绝终止时进程通常也在退出中。
        }
        try{target.Dispose();}
        catch(Exception){
            // 故障收尾：释放句柄失败也不掩盖原始错误。
        }
    }

    // 发现顺序：latest-ai.txt 指向的构建产物 → 程序目录下最新的 Ai-QiDesk-*.exe。
    internal static string Locate(){
        string root=AppDomain.CurrentDomain.BaseDirectory;
        string pointer=Path.Combine(root,"latest-ai.txt");
        try{
            if(File.Exists(pointer)){
                string pointed=File.ReadAllText(pointer).Trim();
                if(pointed.Length>0&&File.Exists(pointed))return pointed;
            }
        }catch(IOException){
            // 指针不可读时退回通配发现。
        }catch(ArgumentException){
            // 指针内容不是合法路径时同样退回。
        }
        try{
            string fallback=Directory.GetFiles(root,"Ai-QiDesk-*.exe")
                .OrderByDescending(f=>f,StringComparer.OrdinalIgnoreCase).FirstOrDefault();
            if(fallback!=null)return fallback;
        }catch(IOException){
            // 枚举失败按组件缺失处理。
        }catch(UnauthorizedAccessException){
            // 枚举失败按组件缺失处理。
        }
        throw new Exception("找不到 AI 组件（Ai-QiDesk-*.exe），请重新构建栖桌。");
    }
}
}
