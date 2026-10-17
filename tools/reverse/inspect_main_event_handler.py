from pathlib import Path
import json
from mcp_client import Client
out=Path(__file__).resolve().parents[2]/'research/decompiled/native/interactions'
c=Client('http://127.0.0.1:8081/mcp');c.initialize()
jobs=[('main-handler-info','get_function_by_address',{'address':'0x18005e900'}),('main-handler-calls','get_function_calls',{'address':'0x18005e900','direction':'callees','limit':500}),('main-handler-entry','disassemble_function',{'address':'0x18005e900','limit':150}),('listview-subclass','decompile_function_by_address',{'address':'0x18006b200'})]
for name,tool,args in jobs:
    result=c.call(tool,args)
    (out/(name+'.json')).write_text(json.dumps(result,indent=2,ensure_ascii=False),encoding='utf-8')
    text='\n'.join(x.get('text','') for x in result.get('content',[]) if x.get('type')=='text')
    (out/(name+'.txt')).write_text(text,encoding='utf-8',newline='\n')
    body=result.get('structuredContent')
    if isinstance(body,dict) and body.get('code'):(out/(name+'.c')).write_text(body['code'].replace('\r',''),encoding='utf-8',newline='\n')
    print(name+': '+('error' if result.get('isError') else 'ok'),flush=True)
c.close()
