# Account Approval Component - Testing Checklist

**Author:** SID:2412494
**Created:** 2025-11-07
**Component:** PendingApprovals.razor
**Purpose:** Manual testing guide for account approval workflow

---

## Prerequisites: Start the Application

```bash
# From web-implementation/ directory
dotnet watch run
```

Then navigate to `https://localhost:5001` (or the port shown in terminal)

---

## Test Cases

### ✅ Test 1: Verify Patient Auto-Approval

**Goal**: Confirm patients are auto-approved and never appear in pending approvals

**Steps:**
1. Navigate to `/account-creation`
2. Fill in:
   - First Name: `Test`
   - Last Name: `Patient`
   - Email: `testpatient@example.com`
   - Password: `Password123!`
   - Account Type: **Patient**
3. Complete registration

**Expected Result:**
- [ ] Account creation succeeds
- [ ] You can immediately log in with these credentials
- [ ] Patient account does NOT appear in `/admin/approvals`

---

### ✅ Test 2: Verify Clinician Requires Approval

**Goal**: Confirm clinician accounts cannot log in until approved

**Steps:**
1. **Create a clinician account:**
   - Log out if logged in
   - Navigate to `/account-creation`
   - Fill in:
     - First Name: `Test`
     - Last Name: `Clinician`
     - Email: `testclinician@example.com`
     - Password: `Password123!`
     - Account Type: **Clinician**
   - Complete registration

2. **Try to log in (should fail):**
   - Navigate to `/login`
   - Enter: `testclinician@example.com` / `Password123!`

3. **Log in as admin:**
   - Use an existing admin account

4. **View pending approvals:**
   - Navigate to `/admin/approvals`

**Expected Results:**
- [ ] Login fails with message: "Your account is pending approval. Please contact an administrator."
- [ ] See "Test Clinician" in the pending approvals table
- [ ] Shows: Name, Email, Type badge (green "Clinician"), and "Requested" date

---

### ✅ Test 3: Pure Blazor Confirmation Workflow

**Goal**: Test inline confirmation UI

**Steps:**
1. In `/admin/approvals` page, click "Approve" button next to Test Clinician
2. Observe the confirmation UI
3. Click "Cancel"
4. Click "Approve" again, then "Confirm"

**Expected Results:**
- [ ] Approve button disappears when clicked
- [ ] Confirmation message appears: "Approve Test's account?"
- [ ] Two new buttons appear: "Confirm" (green) and "Cancel" (gray)
- [ ] Cancel button restores original "Approve" button
- [ ] Confirm button shows success message: "User approved successfully"
- [ ] User disappears from pending list
- [ ] Pending count decreases

---

### ✅ Test 4: Verify Approved User Can Log In

**Goal**: Confirm approval enables login

**Steps:**
1. Log out from admin account
2. Log in as the approved clinician:
   - Email: `testclinician@example.com`
   - Password: `Password123!`

**Expected Result:**
- [ ] Login succeeds
- [ ] Redirects to `/clinician/dashboard`

---

### ✅ Test 5: Verify Admin Account Requires Approval

**Goal**: Same workflow for admin accounts

**Steps:**
1. **Create an admin account:**
   - Log out
   - Navigate to `/account-creation`
   - Fill in:
     - First Name: `Test`
     - Last Name: `Admin`
     - Email: `testadmin@example.com`
     - Password: `Password123!`
     - Account Type: **Administrator**

2. **Try to log in (should fail)**

3. **Log in as existing admin and approve:**
   - Navigate to `/admin/approvals`
   - Approve the account

**Expected Results:**
- [ ] Login fails with "pending approval" message
- [ ] Test Admin appears in approvals with black "Admin" badge
- [ ] Same confirmation workflow as clinician
- [ ] Approval succeeds

---

### ✅ Test 6: Race Condition / Idempotency

**Goal**: Verify duplicate approvals are handled gracefully

**Note**: Requires database access

**Steps:**
1. In database, manually reset an approved user:
   ```sql
   UPDATE "AspNetUsers"
   SET "ApprovedAt" = NULL, "ApprovedBy" = NULL
   WHERE "Email" = 'testclinician@example.com';
   ```
2. Approve the user in UI
3. Try approving again (by refreshing and clicking before removal)

**Expected Result:**
- [ ] Second approval shows "User already approved" message
- [ ] No database errors or exceptions

---

### ✅ Test 7: Audit Trail Verification

**Goal**: Verify ApprovedBy foreign key is set correctly

**Check in database:**
```sql
SELECT
    u."FirstName",
    u."LastName",
    u."Email",
    u."ApprovedAt",
    approver."FirstName" AS "ApprovedByFirstName",
    approver."Email" AS "ApprovedByEmail"
FROM "AspNetUsers" u
LEFT JOIN "AspNetUsers" approver ON u."ApprovedBy" = approver."Id"
WHERE u."Email" = 'testclinician@example.com';
```

**Expected Results:**
- [ ] ApprovedAt timestamp is set
- [ ] ApprovedBy foreign key references the admin who approved
- [ ] ApprovedByFirstName and ApprovedByEmail match the admin account

---

### ✅ Test 8: Empty State

**Goal**: Verify UI when no pending approvals

**Steps:**
1. Approve all pending users
2. Navigate to `/admin/approvals`

**Expected Results:**
- [ ] Message displays: "No pending approvals"
- [ ] Subtext displays: "All accounts have been reviewed."
- [ ] No table is shown
- [ ] Pending count is 0 or not displayed

---

### ✅ Test 9: Responsive Design

**Goal**: Test mobile layout

**Steps:**
1. Resize browser to mobile width (< 768px)
2. Navigate to `/admin/approvals`
3. Test approve workflow on mobile

**Expected Results:**
- [ ] Table remains readable
- [ ] Confirmation buttons stack vertically
- [ ] Action buttons expand to full width
- [ ] Navigation tabs remain accessible

---

### ✅ Test 10: Navigation Integration

**Goal**: Verify navigation tab works

**Steps:**
1. Log in as admin
2. In admin header, click "Approvals" tab
3. Navigate to other tabs and back

**Expected Results:**
- [ ] Clicking "Approvals" navigates to `/admin/approvals`
- [ ] "Approvals" tab highlighted as active when on page
- [ ] Tab styling matches other admin pages

---

## Common Issues & Solutions

| Issue | Solution |
|-------|----------|
| No admin account exists | Create one via `/account-creation` with Type: Administrator, then manually approve in database |
| Database not migrated | Run `dotnet ef database update` |
| Port conflicts | Check the port in terminal output |
| Role not assigned | Roles created automatically on first login |
| Null reference on ApprovedBy | Ensure migration `20251107130914_AddApprovedByToUsers` is applied |

---

## Quick Test Summary Checklist

**Core Functionality:**
- [ ] Patient accounts auto-approved
- [ ] Clinician accounts require approval
- [ ] Admin accounts require approval
- [ ] Unapproved users blocked from login
- [ ] Approved users can log in successfully

**UI/UX:**
- [ ] Inline confirmation workflow works
- [ ] Cancel button restores approve button
- [ ] Confirm button approves and removes user
- [ ] Success/error messages display correctly
- [ ] Empty state displays when no pending approvals
- [ ] Pending count badge accurate

**Data Integrity:**
- [ ] ApprovedAt timestamp set correctly
- [ ] ApprovedBy foreign key set correctly
- [ ] Race condition handled gracefully
- [ ] Audit logging working (check application logs)

**Integration:**
- [ ] Navigation tab works and highlights
- [ ] Component loads without errors
- [ ] Styling matches Users.razor pattern
- [ ] Responsive design works on mobile

---

## Database Verification Queries

### View All Pending Approvals
```sql
SELECT "FirstName", "LastName", "Email", "UserType", "CreatedAt", "ApprovedAt"
FROM "AspNetUsers"
WHERE "ApprovedAt" IS NULL
  AND "DeactivatedAt" IS NULL
  AND ("UserType" = 'admin' OR "UserType" = 'clinician')
ORDER BY "CreatedAt" ASC;
```

### View Approval History
```sql
SELECT
    u."FirstName" || ' ' || u."LastName" AS "User",
    u."Email",
    u."UserType",
    u."ApprovedAt",
    approver."FirstName" || ' ' || approver."LastName" AS "ApprovedBy"
FROM "AspNetUsers" u
LEFT JOIN "AspNetUsers" approver ON u."ApprovedBy" = approver."Id"
WHERE u."ApprovedAt" IS NOT NULL
ORDER BY u."ApprovedAt" DESC;
```

### Manually Approve User (Emergency)
```sql
-- Replace with actual admin user ID
UPDATE "AspNetUsers"
SET "ApprovedAt" = NOW(),
    "ApprovedBy" = 'admin-user-guid-here'
WHERE "Email" = 'user@example.com';
```

### Reset User to Pending (For Testing)
```sql
UPDATE "AspNetUsers"
SET "ApprovedAt" = NULL,
    "ApprovedBy" = NULL
WHERE "Email" = 'testuser@example.com';
```

---

## Test Results Log

**Date Tested:** _________________
**Tested By:** _________________
**Build/Commit:** _________________

**Overall Result:** ✅ PASS / ❌ FAIL

**Notes:**
-
-
-

**Issues Found:**
1.
2.
3.

**Follow-up Actions:**
- [ ]
- [ ]
- [ ]
