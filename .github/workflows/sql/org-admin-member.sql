-- Ensure a person is a member of an organization with a given role.
--
-- Run by .github/workflows/org-admin-member.yml, which declares @Email, @OrgName and
-- @Role above this file. Idempotent: run twice, the second run changes nothing.
--
-- The repository is public, and so is the run's log. So this prints what it did and
-- counts, never an email, a member list or the names of the organizations there are.

SET NOCOUNT ON;

DECLARE @OrgCount int = (SELECT COUNT(*) FROM Organizations WHERE REPLACE(Name,' ','') = REPLACE(@OrgName,' ',''));
IF @OrgCount <> 1
BEGIN
  PRINT 'ABORT: expected exactly one organization with the name given, found ' + CAST(@OrgCount AS varchar(10)) + '. Nothing was changed.';
  THROW 50000, 'Organization not uniquely identified', 1;
END
DECLARE @OrgId uniqueidentifier = (SELECT TOP 1 Id FROM Organizations WHERE REPLACE(Name,' ','') = REPLACE(@OrgName,' ',''));
PRINT 'Organization found.';

DECLARE @UserId uniqueidentifier = (SELECT TOP 1 Id FROM Users WHERE LOWER(Email) = LOWER(@Email));

-- The owner's role is not this workflow's to change. The app refuses to ("Cannot change
-- the owner's role"), the role input has no Owner, and nothing in the app can make
-- anyone owner again: run for the owner with the default role, it made them a
-- read-only Viewer of their own organization for good.
IF @UserId IS NOT NULL AND (
     EXISTS (SELECT 1 FROM Organizations WHERE Id = @OrgId AND OwnerUserId = @UserId)
  OR EXISTS (SELECT 1 FROM OrganizationMembers WHERE OrganizationId = @OrgId AND UserId = @UserId AND Role = 'Owner'))
BEGIN
  PRINT 'ABORT: this person is the owner of the organization, and the owner''s role is not changed here. Nothing was changed.';
  THROW 50001, 'Refusing to change the owner''s role', 1;
END

IF @UserId IS NOT NULL
BEGIN
  IF EXISTS (SELECT 1 FROM OrganizationMembers WHERE OrganizationId = @OrgId AND UserId = @UserId)
  BEGIN
    UPDATE OrganizationMembers SET Role = @Role WHERE OrganizationId = @OrgId AND UserId = @UserId;
    PRINT 'RESULT: already a member - role set to ' + @Role + '.';
  END
  ELSE
  BEGIN
    INSERT INTO OrganizationMembers (OrganizationId, UserId, Role, JoinedAtUtc)
    VALUES (@OrgId, @UserId, @Role, SYSUTCDATETIME());
    PRINT 'RESULT: added to the organization with role ' + @Role + '.';
  END
  DELETE FROM Invites WHERE OrganizationId = @OrgId AND LOWER(Email) = LOWER(@Email) AND AcceptedByUserId IS NULL;
  IF @@ROWCOUNT > 0 PRINT 'Cleaned up now-moot pending invite(s).';
END
ELSE
BEGIN
  IF EXISTS (SELECT 1 FROM Invites WHERE OrganizationId = @OrgId AND LOWER(Email) = LOWER(@Email) AND AcceptedByUserId IS NULL)
  BEGIN
    UPDATE Invites SET ExpiresAtUtc = DATEADD(day, 7, SYSUTCDATETIME())
    WHERE OrganizationId = @OrgId AND LOWER(Email) = LOWER(@Email) AND AcceptedByUserId IS NULL;
    PRINT 'RESULT: no account with this email yet - existing pending invite refreshed for 7 more days.';
  END
  ELSE
  BEGIN
    INSERT INTO Invites (Id, OrganizationId, Email, Token, ExpiresAtUtc, CreatedAtUtc)
    VALUES (NEWID(), @OrgId, LOWER(@Email),
            LOWER(REPLACE(CAST(NEWID() AS varchar(36)),'-','') + REPLACE(CAST(NEWID() AS varchar(36)),'-','')),
            DATEADD(day, 7, SYSUTCDATETIME()), SYSUTCDATETIME());
    PRINT 'RESULT: no account with this email yet - pending invite created. It appears on their Organizations page once they sign up with this email. Accepting makes them a Member; re-run this workflow afterwards to set the role.';
  END
END

DECLARE @Members int = (SELECT COUNT(*) FROM OrganizationMembers WHERE OrganizationId = @OrgId);
DECLARE @Pending int = (SELECT COUNT(*) FROM Invites WHERE OrganizationId = @OrgId AND AcceptedByUserId IS NULL);
PRINT 'The organization now has ' + CAST(@Members AS varchar(10)) + ' member(s) and ' + CAST(@Pending AS varchar(10)) + ' pending invite(s).';
