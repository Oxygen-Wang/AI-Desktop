from pathlib import Path
root=Path(__file__).resolve().parents[1]
p=root/'src/McpBridge.cs';s=p.read_text(encoding='utf-8-sig')
s=s.replace('string previous=Store.Json.Serialize(store.Data);','if(name=="qidesk_get_settings")return AiSettings.Get(store);if(name=="qidesk_search_files"){string mode=Text(args,"mode");if(mode!=""&&mode!="search"&&mode!="modified")throw new Exception("Invalid search mode.");var found=FileSearch.Query(store,Text(args,"query"),mode=="modified");return new {provider=found.Provider,message=found.Message,results=found.Items.Select(p=>new {name=p.Name,path=p.Path,size=p.Bytes,modified=p.Changed}).ToArray()};}string previous=Store.Json.Serialize(store.Data);')
s=s.replace('if(name=="qidesk_connect_workspace"){','if(AiSettings.Apply(controller,name,args,out result)){rebuild=true;}else if(name=="qidesk_connect_workspace"){')
s=s.replace('store.Save();if(rebuild)controller.Rebuild();controller.Refresh();return result;','store.Save();if(rebuild)controller.Rebuild();controller.UpdateDesktopIntegration();controller.Refresh();return result;')
s=s.replace('Definition("qidesk_add_widget","Add an instance of an installed plugin, including clock or calendar.",false,new[]{"pluginId"},"pluginId")};','Definition("qidesk_add_widget","Add an instance of an installed plugin, including clock or calendar.",false,new[]{"pluginId"},"pluginId")}.Concat(AiSettings.Tools()).ToArray();')
old='if(type=="array"){var array='
new='if(type=="object"){var values=args[key] as Dictionary<string,object>;if(values==null||values.Count==0)throw new Exception("Invalid settings object.");var allowed=(Dictionary<string,object>)((Dictionary<string,object>)def)["properties"];foreach(var pair in values){object member;if(!allowed.TryGetValue(pair.Key,out member))throw new Exception("Unexpected setting: "+pair.Key);string memberType=Convert.ToString(((Dictionary<string,object>)member)["type"]);if(memberType=="boolean"&&!(pair.Value is bool)||memberType=="string"&&(!(pair.Value is string)||((string)pair.Value).Length>2000)||((memberType=="integer"||memberType=="number")&&(pair.Value==null||pair.Value is bool||pair.Value is string)))throw new Exception("Invalid setting value: "+pair.Key);}}if(type=="array"){var array='
assert old in s;s=s.replace(old,new)
p.write_text(s,encoding='utf-8-sig')
print('AI settings and Everything MCP tools wired')
