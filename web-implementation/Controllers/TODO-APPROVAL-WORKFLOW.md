# TODO: Account Approval Workflow Integration

**Author:** SID:2412494
**Created:** 2025-11-07
**Priority:** HIGH - Implement AFTER approval component is built and tested

---

## Overview

These changes integrate the account approval workflow with the authentication system. They should be implemented **LAST** to allow testing of the approval component without blocking developer logins.

---

## Task 1: Patient Auto-Approval on Registration

**File:** `Controllers/AccountController.cs`
**Method:** `Register()` (line 152)
**Location:** After user creation, before role assignment (around line 178)

### Changes Required

```csharp
var result = await _userManager.CreateAsync(user, request.Password);

if (result.Succeeded)
{
    _logger.LogInformation("User {UserId} created successfully", user.Id);

    // TODO: Auto-approve patient accounts immediately
    // Admin and clinician accounts require manual approval
    if (user.UserType == "patient")
    {
        user.ApprovedAt = DateTime.UtcNow;
        // No ApprovedBy for auto-approved patients (or set to system/null)
        await _userManager.UpdateAsync(user);
        _logger.LogInformation("Patient account {UserId} auto-approved", user.Id);
    }
    else
    {
        _logger.LogInformation("Admin/Clinician account {UserId} pending approval", user.Id);
    }

    // Assign user to role based on UserType
    // ... (rest of registration code)
}
```

### Rationale
- Patients should have immediate access to the system upon registration
- Admins and clinicians require manual vetting before accessing sensitive data
- Prevents bottleneck where patients can't use the system while waiting for admin approval

---

## Task 2: Block Unapproved Users from Logging In

**File:** `Controllers/AccountController.cs`
**Method:** `Login()` (line 54)
**Location:** After deactivation check, before lockout check (around line 85)

### Changes Required

```csharp
// Check if deactivated
if (user.DeactivatedAt != null)
{
    _logger.LogWarning("Login attempt for deactivated user: {UserId}", user.Id);
    return Redirect("/login?error=" + Uri.EscapeDataString("This account has been deactivated. Please contact support."));
}

// TODO: Check if account is approved
// Only enforce approval for non-patient accounts (patients are auto-approved on registration)
if (user.ApprovedAt == null)
{
    _logger.LogWarning("Login attempt for unapproved user: {UserId} (Type: {UserType})", user.Id, user.UserType);

    // Provide user-friendly message based on account type
    var message = user.UserType == "patient"
        ? "Your account is pending activation. Please contact support."
        : "Your account is pending administrator approval. You will receive notification when approved.";

    return Redirect("/login?error=" + Uri.EscapeDataString(message));
}

// Check if account is locked out
// ... (rest of login code)
```

### Rationale
- Security: Prevents unapproved admin/clinician accounts from accessing the system
- User Experience: Provides clear feedback about why login failed
- Data Protection: Ensures only vetted users can access sensitive medical data

---

## Task 3: Email Notification on Approval (Optional Enhancement)

**File:** `Services/UserManagementService.cs`
**Method:** `ApproveUserAsync()` (to be implemented)
**Location:** After successful approval

### Future Enhancement

```csharp
// After user approval succeeds
_logger.LogInformation("User {UserId} approved by admin {AdminId}", userId, approvedByAdminId);

// TODO: Send email notification to user
// await _emailService.SendAccountApprovedEmailAsync(user.Email, user.FullName);

return (true, "User approved successfully");
```

### Rationale
- Improves user experience by notifying them when they can access the system
- Reduces support burden (users won't keep trying to log in)
- Professional communication flow

---

## Testing Strategy

### Phase 1: Test Approval Component WITHOUT These Changes
1. Build approval component
2. Test approving users
3. Verify `ApprovedAt` and `ApprovedBy` are set correctly in database
4. Can still log in with unapproved accounts for testing

### Phase 2: Implement Auto-Approval (Task 1)
1. Register new patient account
2. Verify `ApprovedAt` is set immediately in database
3. Verify patient can log in immediately
4. Register new admin/clinician account
5. Verify `ApprovedAt` is NULL
6. Can still log in for testing (Task 2 not implemented yet)

### Phase 3: Implement Login Blocking (Task 2)
1. Create unapproved admin account
2. Attempt login → should be blocked with error message
3. Approve account via admin dashboard
4. Attempt login → should succeed
5. Verify patients still auto-approve and can log in immediately

---

## Database Queries for Verification

```sql
-- Check patient auto-approval is working
SELECT user_id, email, user_type, approved_at, created_at
FROM "AspNetUsers"
WHERE user_type = 'patient'
ORDER BY created_at DESC
LIMIT 10;
-- All patients should have approved_at set

-- Check admin/clinician approval workflow
SELECT user_id, email, user_type, approved_at, approved_by, created_at
FROM "AspNetUsers"
WHERE user_type IN ('admin', 'clinician')
  AND approved_at IS NULL
ORDER BY created_at DESC;
-- Should show pending approval accounts

-- Verify approval history
SELECT
    u.email,
    u.user_type,
    u.approved_at,
    a.email as approved_by_email
FROM "AspNetUsers" u
LEFT JOIN "AspNetUsers" a ON u.approved_by = a.user_id
WHERE u.approved_at IS NOT NULL
  AND u.user_type != 'patient';
-- Should show who approved each admin/clinician account
```

---

## Security Considerations

1. **Defense in Depth:** Even with login blocking, authorization attributes on pages provide secondary protection
2. **Audit Trail:** Both `ApprovedAt` timestamp and `ApprovedBy` foreign key track approval actions
3. **Race Condition:** Approval service method is idempotent (checks if already approved)
4. **Patient Exception:** Auto-approval for patients is intentional (low-risk user type)

---

## Related Files

- `Components/Pages/Admin/Approvals/PendingApprovals.razor` - Approval component
- `Services/UserManagementService.cs` - Service with `ApproveUserAsync()` method
- `Models/ApplicationUser.cs` - User model with `ApprovedAt` and `ApprovedBy` fields
- `Data/Migrations/AddApprovedByToUsers.cs` - Migration for `ApprovedBy` field (to be created)

---

## Implementation Order

1. ✅ Create `ApprovedBy` migration
2. ✅ Build approval component (`PendingApprovals.razor`)
3. ✅ Test approval workflow (can log in even if unapproved)
4. ⬜ **Implement Task 1** (patient auto-approval)
5. ⬜ **Test Task 1** (verify patients auto-approve)
6. ⬜ **Implement Task 2** (block unapproved logins)
7. ⬜ **Test Task 2** (verify blocking works, approved users can log in)
8. ⬜ **Optional: Implement Task 3** (email notifications)

---

**DO NOT implement these changes until the approval component is fully functional and tested!**
