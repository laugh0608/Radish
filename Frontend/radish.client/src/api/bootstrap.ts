import { apiGet, apiPost, createApiResponseError } from '@radish/http';
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

export async function getBootstrapStatus(fallbackMessage: string): Promise<BootstrapStatus> {
  const response = await apiGet<BootstrapStatus>('/api/v1/Bootstrap/Status', bootstrapApiOptions);
  if (!response.ok || !response.data) {
    throw createApiResponseError(response, fallbackMessage);
  }

  return response.data;
}

export async function createFirstAdministrator(
  request: BootstrapCreateAdminRequest,
  fallbackMessage: string,
): Promise<BootstrapAdminCreated> {
  const response = await apiPost<BootstrapAdminCreated>(
    '/api/v1/Bootstrap/CreateFirstAdministrator',
    request,
    bootstrapApiOptions,
  );
  if (!response.ok || !response.data) {
    throw createApiResponseError(response, fallbackMessage);
  }

  return response.data;
}
