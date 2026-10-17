"""Minimal Streamable HTTP client for read-only local RE tool calls."""
import json,urllib.request

class Client:
    def __init__(self,url):self.url=url;self.session=None;self.counter=0
    def rpc(self,method,params=None,notification=False):
        self.counter+=1
        body={'jsonrpc':'2.0','method':method}
        if not notification:body['id']=self.counter
        if params is not None:body['params']=params
        headers={'Accept':'application/json, text/event-stream','Content-Type':'application/json','MCP-Protocol-Version':'2025-06-18'}
        if self.session:headers['Mcp-Session-Id']=self.session
        req=urllib.request.Request(self.url,data=json.dumps(body).encode(),headers=headers)
        with urllib.request.urlopen(req,timeout=100) as response:
            self.session=response.headers.get('mcp-session-id',self.session)
            text=response.read().decode()
        if not text:return {}
        messages=[json.loads(line[5:].strip()) for line in text.splitlines() if line.startswith('data:')]
        result=messages[-1] if messages else json.loads(text)
        if 'error' in result:raise RuntimeError(result['error'])
        return result.get('result',{})
    def initialize(self):
        result=self.rpc('initialize',{'protocolVersion':'2025-06-18','capabilities':{},'clientInfo':{'name':'Codex-Fences-RE','version':'1'}})
        self.rpc('notifications/initialized',notification=True)
        return result
    def tools(self):return self.rpc('tools/list')['tools']
    def call(self,name,arguments):return self.rpc('tools/call',{'name':name,'arguments':arguments})
    def close(self):
        if not self.session:return
        try:urllib.request.urlopen(urllib.request.Request(self.url,method='DELETE',headers={'Mcp-Session-Id':self.session,'MCP-Protocol-Version':'2025-06-18'}),timeout=5).close()
        except Exception:pass

if __name__=='__main__':
    import argparse,pathlib
    p=argparse.ArgumentParser();p.add_argument('url');p.add_argument('--tool');p.add_argument('--args',default='{}');p.add_argument('--out');a=p.parse_args()
    c=Client(a.url);print(json.dumps(c.initialize(),ensure_ascii=False)[:400]);ts=c.tools()
    if a.tool:
        result=c.call(a.tool,json.loads(a.args))
        if a.out:pathlib.Path(a.out).write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
        print(json.dumps(result,ensure_ascii=False,indent=2)[:9000])
    else:
        for t in ts:print(t['name']+' '+json.dumps(t.get('inputSchema',{}),ensure_ascii=False))
    c.close()
