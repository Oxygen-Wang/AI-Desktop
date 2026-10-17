"""Read-only export of Fences organization data; never writes to its registry keys."""
from pathlib import Path
import winreg,json,base64

ROOT=Path(__file__).resolve().parents[2]
BASE=r'Software\Stardock\Fences'
OUT=ROOT/'research/evidence/fences-config.json'

def read_key(name):
    result={}
    try:
        with winreg.OpenKey(winreg.HKEY_CURRENT_USER,BASE+'\\'+name,0,winreg.KEY_READ) as key:
            i=0
            while True:
                try:n,v,t=winreg.EnumValue(key,i)
                except OSError:break
                if isinstance(v,bytes):v={'base64':base64.b64encode(v).decode()}
                result[n]={'value':v,'registry_type':t};i+=1
    except FileNotFoundError:pass
    return result

GROUP_FIELDS=['id','x','y','width','height','scroll','title','monitor_name','monitor_x','monitor_y','monitor_width','monitor_height','sort_method','sort_flags','opacity','page','folder','edge','last_edge','rolled','last_rolled_dock','view_flags','guid']
ICON_FIELDS=['group_id','absolute_x','absolute_y','relative_x','relative_y','use_absolute','monitor_relative_x','monitor_relative_y','last_monitor','times_opened','added_date_low','added_date_high','associated_program','page','last_clicked_low','last_clicked_high']

def parse_groups(raw):
    groups=[];errors=[]
    for name,item in raw.items():
        if not name.isdigit() or not isinstance(item['value'],str):continue
        parts=item['value'].split('|')
        if len(parts)<12:errors.append({'key':name,'error':'Less than 12 fields'});continue
        group={'registry_value':name}
        for i,field in enumerate(GROUP_FIELDS):
            if i>=len(parts):break
            value=parts[i]
            if field in ('title','monitor_name','folder','guid'):value=value.replace('`',' ').strip()
            else:
                try:value=int(value)
                except ValueError:pass
            group[field]=value
        groups.append(group)
    return groups,errors

def main():
    selected=['Groups','Rules','Icons','Settings','Skinning','ViewStates','NavHistory']
    raw={key:read_key(key) for key in selected}
    groups,errors=parse_groups(raw['Groups'])
    rules=[]
    for n in range(50):
        name=f'{n:02d}'
        if name not in raw['Rules'] or name+'_Target' not in raw['Rules']:continue
        rule=raw['Rules'][name]['value'];target=raw['Rules'][name+'_Target']['value']
        if not isinstance(rule,str) or not isinstance(target,str):continue
        if not rule or not target:continue
        data=rule.split('|');fields=target.split('|')
        rules.append({'index':n,'name':data[0],'criterion_serializations':[s for s in data[1:] if s],
                      'enabled':fields[0]!='0','target_group_id':fields[1] if len(fields)>1 else None,
                      'place_at_end':fields[2]!='0' if len(fields)>2 else True})
    icons=[]
    for path,item in raw['Icons'].items():
        if not isinstance(item['value'],str):continue
        fields=item['value'].split('|')
        if len(fields)<6:continue
        icon={'path':path}
        for name,value in zip(ICON_FIELDS,fields):
            try:icon[name]=int(value)
            except ValueError:icon[name]=value
        icons.append(icon)
    export={'source':'HKCU\\'+BASE,'read_only':True,'groups':groups,'rules':rules,'icons':icons,'parse_errors':errors,'raw':raw}
    OUT.parent.mkdir(parents=True,exist_ok=True)
    OUT.write_text(json.dumps(export,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps({'groups':len(groups),'rules':len(rules),'icons':len(icons),'parse_errors':len(errors),'output':str(OUT)},ensure_ascii=False))

if __name__=='__main__':main()
