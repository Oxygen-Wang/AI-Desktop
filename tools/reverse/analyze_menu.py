from pathlib import Path
import json
from mcp_client import Client
OUT=Path(__file__).resolve().parents[2]/'research/decompiled/native'
c=Client('http://127.0.0.1:8082/mcp');c.initialize()
for name,tool,args in [('FencesMenu64-program-info','get_program_info',{}),('FencesMenu64-exports','list_exports',{'limit':100}),('FencesMenu64-COM-class-factory','decompile_function',{'name':'DllGetClassObject'}),('FencesMenu64-register','decompile_function',{'name':'DllRegisterServer'}),('FencesMenu64-context-strings','list_strings',{'filter':'ContextMenu','limit':30})]:
    result=c.call(tool,args)
    (OUT/(name+'.json')).write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    text='\n'.join(x.get('text','') for x in result.get('content',[]) if x.get('type')=='text')
    (OUT/(name+'.txt')).write_text(text,encoding='utf-8',newline='\n')
    if result.get('isError'):raise RuntimeError(text)
    body=json.loads(text)
    if body.get('code'):(OUT/(name+'.c')).write_text(body['code'].replace('\r',''),encoding='utf-8',newline='\n')
    print('OK '+name+' '+str(len(text)),flush=True)
c.close()
