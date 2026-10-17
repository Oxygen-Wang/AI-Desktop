import json
from pathlib import Path
from mcp_client import Client
c=Client('http://127.0.0.1:3001/mcp');c.initialize()
result=c.call('decompile_type',{'assemblyPath':r'C:\Program Files (x86)\Stardock\Fences\Fences.exe','typeName':'SnapshotInfo'})
out=Path(__file__).resolve().parents[2]/'research/decompiled/managed'
(out/'SnapshotInfo.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
text='\n'.join(x.get('text','') for x in result.get('content',[]) if x.get('type')=='text')
(out/'SnapshotInfo.txt').write_text(text,encoding='utf-8',newline='\n')
print(text[:4000]);c.close()
