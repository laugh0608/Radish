import { getSignalrHubUrl } from '@/config/env';
import { tokenService } from './tokenService';
import { selectHubCredential, type HubName } from './webHubPolicy';

export async function resolveHubSession(name: HubName, authenticated: boolean) {
  const mode = await tokenService.resolveSessionMode();
  const credential = selectHubCredential(name, mode, authenticated || Boolean(tokenService.getAccessToken()));
  if (!credential) return null;

  const url = credential === 'web'
    ? `${window.location.origin}/hub/web/${name}`
    : `${getSignalrHubUrl()}/hub/${name}`;
  return {
    credential,
    url,
    accessTokenFactory: async () => {
      if (credential === 'web') return await tokenService.getWebToken() ?? '';
      if (credential === 'legacy' && tokenService.sessionMode === 'legacy') {
        return await tokenService.getValidAccessToken() ?? '';
      }
      return '';
    },
  };
}
