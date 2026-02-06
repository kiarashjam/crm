export type TabId = 'members' | 'invites' | 'requests';

export interface InviteFormState {
  email: string;
  role: 'Admin' | 'Member';
}

export const DEFAULT_INVITE_FORM: InviteFormState = {
  email: '',
  role: 'Member',
};

export type MemberRole = 'Owner' | 'Admin' | 'Member';
