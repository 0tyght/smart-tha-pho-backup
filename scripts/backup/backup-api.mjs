import { createApp } from '../../apps/api/src/composition-root/createHttpApplication.js';
import { config } from '../../apps/api/src/core/config.js';
import { database } from '../../apps/api/src/core/db.js';
import { StandbyExternalTransport } from './StandbyExternalTransport.mjs';

// Infrastructure boundary: standby must never send messages or change LINE menus.
export class BackupHttpRuntime {
  constructor({ application, connection, port }) {
    this.application = application; this.connection = connection; this.port = port;
  }
  async start() {
    await this.connection.query('SELECT 1');
    this.server = this.application.listen(this.port, '127.0.0.1', () => {
      console.log(JSON.stringify({event:'backup_ready',port:this.port,line:'disabled',schedulers:'disabled'}));
    });
    this.server.on('error', error => { console.error(error.message); process.exitCode = 1; });
  }
  async stop() {
    await new Promise(resolve => this.server ? this.server.close(resolve) : resolve());
    await this.connection.end();
  }
}

const transport = new StandbyExternalTransport({fetchImplementation:globalThis.fetch});
globalThis.fetch = transport.fetch.bind(transport);
const runtime = new BackupHttpRuntime({application:createApp(),connection:database,port:config.port});
await runtime.start();
for (const signal of ['SIGINT','SIGTERM']) process.on(signal, async () => { await runtime.stop(); process.exit(0); });
