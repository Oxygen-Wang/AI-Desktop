from pathlib import Path
import json,tomllib,hashlib
ROOT=Path(__file__).resolve().parents[2]
cfg=tomllib.loads((Path.home()/'.codex/config.toml').read_text(encoding='utf-8'))
servers=cfg.get('mcp_servers',{})
assert 'jadx-mcp' not in servers,'jadx-mcp is still registered'
assert 'ilspy-mcp' in servers and 'ghidra-mcp' in servers,'New MCP registrations missing'
native=ROOT/'research/decompiled/native'
for name in ['DesktopDock64','FencesMenu64']:
    info=json.loads((native/(name+'-program-info.txt')).read_text(encoding='utf-8'))
    target=Path(r'C:\Program Files (x86)\Stardock\Fences')/(name+'.dll')
    assert info['sha256']==hashlib.sha256(target.read_bytes()).hexdigest(),'Target SHA mismatch'
    assert info['function_count']>0,'No native functions analyzed'
    print(name+': verified target SHA256; '+str(info['function_count'])+' analyzed functions')
for name in ['FindDesktopWindowSlow','desktop-enumeration-callback','OnProcessAttach','Shell_GetLnkPath','FencesMenu64-COM-class-factory']:
    result=json.loads((native/(name+'.json')).read_text(encoding='utf-8'))
    assert not result.get('isError'),'Critical tool call failed: '+name
    assert (native/(name+'.c')).stat().st_size>0,'Missing pseudocode'
managed=ROOT/'research/decompiled/managed'
for name in ['DesktopDockUI.Layouts.LayoutArea','DesktopDockUI.Simulation.RuleSettings','DesktopDockUI.Simulation.DeskGroupSettings.type','DesktopDockUI.Dialogs.Panes.ToolsPaneA.TakeSnapshot']:
    assert not json.loads((managed/(name+'.json')).read_text(encoding='utf-8')).get('isError'),'Managed decompilation failed'
config=json.loads((ROOT/'research/evidence/fences-config.json').read_text(encoding='utf-8'))
assert config['read_only'] and not config['parse_errors'],'Configuration export invalid'
print('Read-only config: '+str(len(config['groups']))+' groups, '+str(len(config['rules']))+' rules, '+str(len(config['icons']))+' icons')
print('Critical MCP calls, evidence files and registrations verified.')
