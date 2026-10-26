import fs from 'node:fs';
import path from 'node:path';
const root=process.cwd();
const destination=path.join(root,'.runtime/site');
fs.mkdirSync(destination,{recursive:true});
for(const app of ['portal','prms-tsm','waste-management','disaster-management','waterworks-management']) {
  const source=path.join(root,'apps',app,'dist');
  if(!fs.existsSync(path.join(source,'index.html'))) throw Error('Build missing: '+app);
  fs.cpSync(source,app==='portal'?destination:path.join(destination,app),{recursive:true});
}
console.log('Backup static website assembled');
