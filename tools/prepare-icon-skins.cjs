// Development-only conversion using the bundled sharp renderer. No SVG runtime
// or third-party installer is added to QiDesk.
const fs=require('fs'),path=require('path'),crypto=require('crypto'),sharp=require('sharp');
const root=path.resolve(__dirname,'..');
const appEntries={
  chrome:[['chrome.exe'],['Google Chrome','Chrome']],
  edge:[['msedge.exe'],['Microsoft Edge','Edge']],
  firefox:[['firefox.exe'],['Firefox','Mozilla Firefox']],
  qq:[['qq.exe'],['QQ','腾讯QQ']],
  wechat:[['wechat.exe','weixin.exe'],['WeChat','微信']],
  matlab:[['matlab.exe'],['MATLAB']],
  vscode:[['code.exe'],['Visual Studio Code','VS Code']],
  sevenzip:[['7zfm.exe','7zg.exe'],['7-Zip','7-Zip File Manager']],
  word:[['winword.exe'],['Microsoft Word','Word']],
  excel:[['excel.exe'],['Microsoft Excel','Excel']],
  powerpoint:[['powerpnt.exe'],['Microsoft PowerPoint','PowerPoint']],
  cloudmusic:[['cloudmusic.exe'],['网易云音乐','NetEase Cloud Music']]
};
const extensions={word:['.doc','.docx','.rtf'],excel:['.xls','.xlsx','.csv'],powerpoint:['.ppt','.pptx'],document:['.txt','.md','.pdf']};
(async()=>{
  for(const id of ['fluent','papirus']){
    const folder=path.join(root,'code','assets','icon-skins',id);
    const source=JSON.parse(fs.readFileSync(path.join(folder,'SOURCE.json'),'utf8').replace(/^\uFEFF/,''));
    const entries=[];
    for(const item of source.icons){
      const svg=fs.readFileSync(path.join(folder,'svg',item.id+'.svg'));
      if(/(?:href|src)\s*=\s*["'](?!#|data:image\/png;base64,)/i.test(svg.toString()))throw Error('External SVG reference: '+item.id);
      const png=await sharp(svg,{density:192}).resize(128,128,{fit:'contain',background:'#00000000'}).png().toBuffer();
      fs.writeFileSync(path.join(folder,item.id+'.png'),png);
      item.pngSha256=crypto.createHash('sha256').update(png).digest('hex');
      const app=appEntries[item.id]||[[],[]];
      entries.push({Key:item.id,Icon:item.id+'.png',Executables:app[0],Names:app[1],Extensions:extensions[item.id]||[],Kind:item.id==='folder'?'folder':''});
    }
    const pack={Id:id,Name:id==='fluent'?'Fluent 应用':'Papirus 应用',License:'GPL-3.0',Source:'https://github.com/'+source.repo,Revision:source.revision,Entries:entries};
    fs.writeFileSync(path.join(folder,'pack.json'),JSON.stringify(pack,null,2)+'\n');
    fs.writeFileSync(path.join(folder,'SOURCE.json'),JSON.stringify(source,null,2)+'\n');
    console.log(id+': '+entries.length+' PNG assets prepared');
  }
})().catch(e=>{console.error(e);process.exitCode=1;});
