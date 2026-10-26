import test from 'node:test';
import assert from 'node:assert/strict';
import { StandbyExternalTransport } from './StandbyExternalTransport.mjs';
import { NavigationService } from '../../packages/web-core/src/application/NavigationService.js';

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
