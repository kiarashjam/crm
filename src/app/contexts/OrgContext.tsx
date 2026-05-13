import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from 'react';
import {
  listMyOrganizations,
  listMyPendingInvites,
  type Organization,
  type InviteDto,
} from '@/app/api/organizations';
import {
  getCurrentOrganizationId,
  setCurrentOrganizationId,
  getAuthToken,
  AUTH_SESSION_CHANGED,
} from '@/app/lib/auth';
import { isUsingRealApi } from '@/app/api/apiClient';

const DEMO_ORGANIZATIONS: Organization[] = [
  { id: 'demo-org', name: 'Demo Organization', ownerUserId: 'demo', isOwner: true, role: 0 },
  { id: 'demo-acme', name: 'Acme Corp', ownerUserId: 'demo', isOwner: true, role: 0 },
  { id: 'demo-techstart', name: 'TechStart Inc', ownerUserId: 'demo', isOwner: true, role: 0 },
];

type OrgContextValue = {
  organizations: Organization[];
  pendingInvites: InviteDto[];
  currentOrgId: string | null;
  currentOrg: Organization | null;
  setCurrentOrg: (orgId: string | null) => void;
  refreshOrgs: () => Promise<void>;
  loading: boolean;
  hasFetched: boolean;
  /** Demo only: add a new organization and return it. */
  addDemoOrg?: (name: string) => Organization;
};

const OrgContext = createContext<OrgContextValue | null>(null);

export function OrgProvider({ children }: { children: ReactNode }) {
  const [organizations, setOrganizations] = useState<Organization[]>(() =>
    isUsingRealApi() ? [] : [...DEMO_ORGANIZATIONS]
  );
  const [pendingInvites, setPendingInvites] = useState<InviteDto[]>([]);
  const [currentOrgId, setCurrentOrgIdState] = useState<string | null>(() => getCurrentOrganizationId());
  const [loading, setLoading] = useState(false);
  const [hasFetched, setHasFetched] = useState(() => !isUsingRealApi());

  const refreshOrgs = useCallback(async () => {
    if (!isUsingRealApi()) {
      setHasFetched(true);
      return;
    }
    if (!getAuthToken()) {
      setOrganizations([]);
      setPendingInvites([]);
      setCurrentOrgIdState(null);
      setLoading(false);
      setHasFetched(true);
      return;
    }
    setLoading(true);
    try {
      const [orgs, invites] = await Promise.all([
        listMyOrganizations(),
        listMyPendingInvites(),
      ]);
      setOrganizations(orgs);
      setPendingInvites(invites);
      const stored = getCurrentOrganizationId();
      const storedIsValid = Boolean(stored && orgs.some((o) => o.id === stored));
      if (storedIsValid) {
        setCurrentOrgIdState(stored);
      } else {
        if (stored && !storedIsValid) {
          setCurrentOrganizationId(null);
        }
        if (orgs.length > 0) {
          const firstOrgId = orgs[0]?.id;
          if (firstOrgId) {
            setCurrentOrganizationId(firstOrgId);
            setCurrentOrgIdState(firstOrgId);
          }
        } else {
          setCurrentOrgIdState(null);
        }
      }
    } catch {
      setOrganizations([]);
      setPendingInvites([]);
    } finally {
      setLoading(false);
      setHasFetched(true);
    }
  }, []);

  useEffect(() => {
    refreshOrgs();
  }, [refreshOrgs]);

  useEffect(() => {
    const onSessionChanged = () => {
      void refreshOrgs();
    };
    window.addEventListener(AUTH_SESSION_CHANGED, onSessionChanged);
    return () => window.removeEventListener(AUTH_SESSION_CHANGED, onSessionChanged);
  }, [refreshOrgs]);

  const setCurrentOrg = useCallback((orgId: string | null) => {
    setCurrentOrganizationId(orgId);
    setCurrentOrgIdState(orgId);
  }, []);

  const addDemoOrg = useCallback((name: string) => {
    const org: Organization = {
      id: `demo-org-${Date.now()}`,
      name: name.trim() || 'My Organization',
      ownerUserId: 'demo',
      isOwner: true,
      role: 0,
    };
    setOrganizations((prev) => [...prev, org]);
    return org;
  }, []);

  const currentOrg = useMemo(
    () => organizations.find((o) => o.id === (currentOrgId ?? '')) ?? null,
    [organizations, currentOrgId]
  );

  const value = useMemo<OrgContextValue>(
    () => ({
      organizations,
      pendingInvites,
      currentOrgId,
      currentOrg,
      setCurrentOrg,
      refreshOrgs,
      loading,
      hasFetched,
      addDemoOrg: isUsingRealApi() ? undefined : addDemoOrg,
    }),
    [organizations, pendingInvites, currentOrgId, currentOrg, setCurrentOrg, refreshOrgs, loading, hasFetched, addDemoOrg]
  );

  return <OrgContext.Provider value={value}>{children}</OrgContext.Provider>;
}

export function useOrg(): OrgContextValue {
  const ctx = useContext(OrgContext);
  if (!ctx) throw new Error('useOrg must be used within OrgProvider');
  return ctx;
}

export function useOrgOptional(): OrgContextValue | null {
  return useContext(OrgContext);
}
