// What the app lets a member try, from what the server says they may do (or, from an
// older server, from their role), so it offers nothing the server would refuse.
import { describe, expect, it } from 'vitest';
import { isOrgViewer, orgCan, orgPermissions, PERMISSION, type Permission } from './organizations';

const ALL = Object.values(PERMISSION);

describe('what a member may do', () => {
  it('is everything in a personal workspace and for the owner, whatever else is said', () => {
    expect([...orgPermissions(null)].sort()).toEqual([...ALL].sort());
    expect(orgCan({ isOwner: true, role: 1, permissions: [] }, PERMISSION.ManageSettings)).toBe(true);
    expect(orgCan({ isOwner: false, role: 0 }, PERMISSION.VoidMoney)).toBe(true);
  });

  it('is what the server sent when it sent a list', () => {
    const finance = { isOwner: false, role: 1, permissions: [PERMISSION.ManageMoney, PERMISSION.VoidMoney, PERMISSION.ExportRecords] };

    expect(orgCan(finance, PERMISSION.ManageMoney)).toBe(true);
    expect(orgCan(finance, PERMISSION.EditRecords)).toBe(false);
    expect(orgCan(finance, PERMISSION.DeleteRecords)).toBe(false);
    expect(isOrgViewer(finance)).toBe(false);
  });

  it('follows the old role from a server that sends no list, so nobody loses what they had', () => {
    const member = { isOwner: false, role: 1 };
    const kept: Permission[] = [PERMISSION.EditRecords, PERMISSION.DeleteRecords, PERMISSION.ExportRecords, PERMISSION.ManageMoney, PERMISSION.ClubMembers, PERMISSION.FrontDesk];
    expect(kept.every((p) => orgCan(member, p))).toBe(true);
    expect(orgCan(member, PERMISSION.ManageSettings)).toBe(false);
    expect(orgCan(member, PERMISSION.VoidMoney)).toBe(false);

    expect(ALL.every((p) => orgCan({ isOwner: false, role: 2 }, p))).toBe(true);
    expect([...orgPermissions({ isOwner: false, role: 3 })]).toEqual([PERMISSION.ExportRecords]);
  });

  it('is read-only only for someone whose set changes nothing', () => {
    expect(isOrgViewer({ isOwner: false, role: 3, permissions: [PERMISSION.ExportRecords] })).toBe(true);
    expect(isOrgViewer({ isOwner: false, role: 3 })).toBe(true);
    expect(isOrgViewer({ isOwner: false, role: 1, permissions: [PERMISSION.FrontDesk] })).toBe(false);
    expect(isOrgViewer({ isOwner: true, role: 3, permissions: [] })).toBe(false);
    expect(isOrgViewer(null)).toBe(false);
  });
});
