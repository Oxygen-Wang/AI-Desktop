from pathlib import Path
import json
from mcp_client import Client

OUT=Path(__file__).resolve().parents[2]/'research/decompiled/native'
def save(name,result):
    (OUT/(name+'.json')).write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    text='\n'.join(x.get('text','') for x in result.get('content',[]) if x.get('type')=='text')
    (OUT/(name+'.txt')).write_text(text,encoding='utf-8',newline='\n')
    print(('ERROR ' if result.get('isError') else 'OK ')+name+' '+str(len(text)),flush=True)
    if result.get('isError'):return {}
    try:return json.loads(text)
    except ValueError:return {}

if __name__=='__main__':
    c=Client('http://127.0.0.1:8081/mcp');c.initialize()
    for address,label in [('180053c70','desktop-host-fallback'),('1800cf420','load-into-explorer'),('18006f630','desktop-initializer-thread')]:
        save(label,c.call('decompile_function_by_address',{'address':address}))
        save(label+'-calls',c.call('get_function_calls',{'address':address,'direction':'callees','limit':100}))
    imports=save('all-imports',c.call('list_imports',{'limit':500}))
    targets={'SetWindowLongPtrW','SetWindowsHookExW','CallWindowProcW','EnumWindows','SetPropW'}
    for item in imports.get('items',[]):
        if item.get('name') in targets:
            address=item.get('address')
            if address:save(item['name']+'-xrefs',c.call('get_xrefs_to',{'address':address,'limit':50}))
    # Extract readable pseudocode while retaining the raw MCP responses.
    for path in OUT.glob('*.json'):
        result=json.loads(path.read_text(encoding='utf-8'))
        text='\n'.join(x.get('text','') for x in result.get('content',[]) if x.get('type')=='text')
        try:payload=json.loads(text)
        except ValueError:continue
        if 'code' in payload:path.with_suffix('.c').write_text(payload['code'].replace('\r',''),encoding='utf-8',newline='\n')
    c.close()
