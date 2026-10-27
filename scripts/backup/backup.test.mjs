import test from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { StandbyExternalTransport } from './StandbyExternalTransport.mjs';
import { NavigationService } from '../../packages/web-core/src/application/NavigationService.js';
import { SessionStore } from '../../packages/web-core/src/application/SessionStore.js';
import { BrowserWorkspaceRepository } from '../../packages/web-core/src/infrastructure/BrowserWorkspaceRepository.js';

for (const [path, primaryKey, backupKey] of [
  ['../../apps/disaster-management/src/composition-root/createDisasterApplication.js', 'smart_thapho_disaster_prototype_v1', 'smart_thapho_backup_disaster_prototype_v1'],
  ['../../apps/waterworks-management/src/composition-root/createWaterBillingApplication.js', 'smart_thapho_water_billing_prototype_v1', 'smart_thapho_backup_water_billing_prototype_v1'],
]) {
  test('backup prototype data stays separate from primary data: ' + backupKey, () => {
    const source = readFileSync(new URL(path, import.meta.url), 'utf8');
    assert.ok(source.includes('key: "' + backupKey + '"'));
    assert.ok(!source.includes('key: "' + primaryKey + '"'));
    const primaryValue = JSON.stringify({ version: 1, revision: 3, data: { site: 'primary' } });
    const values = new Map([[primaryKey, primaryValue]]);
    const storage = {
      getItem: key => values.get(key) ?? null,
      setItem: (key, value) => values.set(key, value),
    };
    const repository = new BrowserWorkspaceRepository({ storage, key: backupKey, seed: () => ({ site: 'backup' }) });
    assert.deepEqual(repository.load(), { site: 'backup' });
    repository.save({ site: 'backup', updated: true });
    assert.equal(values.get(primaryKey), primaryValue);
    assert.deepEqual(repository.load(), { site: 'backup', updated: true });
  });
}

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
