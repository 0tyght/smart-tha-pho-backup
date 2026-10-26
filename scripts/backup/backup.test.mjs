import test from 'node:test';
import assert from 'node:assert/strict';
import { StandbyExternalTransport } from './StandbyExternalTransport.mjs';
import { NavigationService } from '../../packages/web-core/src/application/NavigationService.js';
import { SessionStore } from '../../packages/web-core/src/application/SessionStore.js';

test('backup login/logout never overwrites or clears the primary website session',()=>{
  const values=new Map([['smart_thapho_access_token','primary-token'],['smart_thapho_active_system','pet']]);
  const storage={getItem:key=>values.get(key)??null,setItem:(key,value)=>values.set(key,value),removeItem:key=>values.delete(key)};
  const session=new SessionStore({storage,transientStorage:null,windowObject:{location:{hostname:'0tyght.github.io'}}});
  assert.equal(session.getAccessToken(),'');
  session.setAccessToken('backup-token');session.setActiveSystem('waste');
  assert.equal(session.getAccessToken(),'backup-token');
  session.clear();
  assert.equal(values.get('smart_thapho_access_token'),'primary-token');
  assert.equal(values.get('smart_thapho_active_system'),'pet');
});

test('standby refuses LINE API calls before any network request',async()=>{
  let calls=0;const transport=new StandbyExternalTransport({fetchImplementation:async()=>{calls++;}});
  for(const host of ['api.line.me','api-data.line.me','access.line.me']){
    await assert.rejects(transport.fetch('https://'+host+'/v2/bot/message/push'),{code:'LINE_DISABLED_ON_BACKUP_SERVER'});
  }
  assert.equal(calls,0);
});
test('standby permits unrelated external services',async()=>{
  const response={ok:true};const transport=new StandbyExternalTransport({fetchImplementation:async()=>response});
  assert.equal(await transport.fetch('https://router.project-osrm.org/route/v1/driving/'),response);
});
for(const [origin,prefix] of [['http://127.0.0.1:4200',''],['https://0tyght.github.io','/smart-tha-pho-backup'],['https://example.trycloudflare.com','']]){
 test('backup static navigation remains on its own origin: '+origin,()=>{
  const location=new URL(origin+prefix+'/waste-management/');
  const nav=new NavigationService({windowObject:{location}});
  assert.equal(nav.getPortalUrl(),origin+prefix+'/');
  assert.equal(nav.getSystemUrl('pet'),origin+prefix+'/prms-tsm/');
  assert.equal(nav.getSystemUrl('water'),origin+prefix+'/waterworks-management/');
 });
}
