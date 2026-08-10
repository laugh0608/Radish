import type { CoinTransaction, UserBalance } from '@/api/coin';
import type { ExperienceData, ExpTransactionData } from '@/api/experience';
import type { PetProfile } from '@/api/pet';
import type { PublicUserProfile, UserBrowseHistoryItem } from '@/api/user';

export type MeDataErrorKey = 'profile' | 'experience' | 'experienceTransactions' | 'assets' | 'browse' | 'pet';

export interface MeDashboardData {
  publicProfile: PublicUserProfile | null;
  pet: PetProfile | null;
  experience: ExperienceData | null;
  expTransactions: ExpTransactionData[];
  balance: UserBalance | null;
  coinTransactions: CoinTransaction[];
  browseHistory: UserBrowseHistoryItem[];
  errors: Partial<Record<MeDataErrorKey, string>>;
  loadedAt: string | null;
}

export const initialMeDashboardData: MeDashboardData = {
  publicProfile: null,
  pet: null,
  experience: null,
  expTransactions: [],
  balance: null,
  coinTransactions: [],
  browseHistory: [],
  errors: {},
  loadedAt: null,
};
