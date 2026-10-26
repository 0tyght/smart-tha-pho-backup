export class StandbyExternalTransport {
  constructor({ fetchImplementation }) { this.fetchImplementation = fetchImplementation; }
  async fetch(input, options) {
    const url = new URL(typeof input === 'string' || input instanceof URL ? input : input.url);
    if (['api.line.me', 'api-data.line.me', 'access.line.me'].includes(url.hostname)) {
      const error = new Error('LINE_DISABLED_ON_BACKUP_SERVER');
      error.code = 'LINE_DISABLED_ON_BACKUP_SERVER';
      throw error;
    }
    return this.fetchImplementation(input, options);
  }
}
