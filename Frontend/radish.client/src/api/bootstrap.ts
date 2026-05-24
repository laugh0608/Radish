import { apiGet, apiPost } from '@radish/http';
import { getApiBaseUrl } from '@/config/env';

export interface BootstrapStatus {
  voRequiresAdminInitialization: boolean;
  voAdministratorExists: boolean;
}

export interface BootstrapCreateAdminRequest {
  loginName: string;
  password: string;
  confirmPassword: string;
  email?: string;
}

export interface BootstrapAdminCreated {
  voUserId: string | number;
  voLoginName: string;
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
