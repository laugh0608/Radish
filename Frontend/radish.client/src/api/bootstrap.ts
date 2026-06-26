import { apiGet, apiPost } from '@radish/http';
import { getApiBaseUrl } from '@/config/env';

export interface BootstrapStatus {
  voRequiresAdminInitialization: boolean;
  voAdministratorExists: boolean;
}

export interface BootstrapCreateAdminRequest {
  displayName: string;
  email: string;
  password: string;
  confirmPassword: string;
}

export interface BootstrapAdminCreated {
  voUserId: string;
  voDisplayName: string;
  voEmail: string;
}

const bootstrapApiOptions = {
  baseUrl: getApiBaseUrl(),
};

export async function getBootstrapStatus() {
  return await apiGet<BootstrapStatus>('/api/v1/Bootstrap/Status', bootstrapApiOptions);
}

export async function createFirstAdministrator(request: BootstrapCreateAdminRequest) {
  return await apiPost<BootstrapAdminCreated>(
    '/api/v1/Bootstrap/CreateFirstAdministrator',
    request,
    bootstrapApiOptions,
  );
}
