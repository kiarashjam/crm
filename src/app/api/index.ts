/** Frontend API layer: always uses real backend when VITE_API_URL is set (never mock). */

export * from './types';
export { messages } from './messages';
export { getApiBaseUrl, isUsingRealApi } from './apiClient';
export {
  login,
  loginWithTwoFactor,
  register,
  me,
  twoFactorSetup,
  twoFactorEnable,
  twoFactorDisable,
  type TwoFactorSetupResponse,
} from './auth';
export * from './contacts';
export * from './deals';
export * from './leads';
export * from './companies';
export * from './pipelines';
export * from './dealStages';
export * from './leadStatuses';
export * from './leadSources';
export * from './tasks';
export * from './activities';
export * from './reporting';
export * from './search';
export * from './templates';
export { getCopyHistory, addToCopyHistory, getCopyHistoryStats } from './copyHistory';
export * from './organizations';
export * from './settings';
export * from './copyGenerator';
export * from './crm';
export * from './webhook';