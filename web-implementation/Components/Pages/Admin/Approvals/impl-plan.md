# Account Approval Component - Implementation Plan

**Author:** SID:2412494
**Created:** 2025-11-07
**Purpose:** Independent component set for approving new non-patient user accounts in the admin dashboard

**NOTE:** This has now been implemented, the plan has been left in for posterity.

---

## Overview

This component provides administrators with the ability to view and approve pending user accounts. It functions as a standalone module within the admin dashboard, displaying users who have registered but not yet been approved (where `ApprovedAt` is NULL).

**Important:** Only clinician and admin accounts require approval. Patient accounts are automatically approved upon registration and will never appear in this component.

---

## Architecture

### Component Structure

```
Components/Pages/Admin/Approvals/
├── impl-plan.md                    (this file)
├── PendingApprovals.razor          (main component)
└── PendingApprovals.razor.css      (scoped styles)
```

### Design Decisions

**1. Single Component Approach**
- One `.razor` component handles all approval functionality
- Rationale: Simple UI with table display and inline approve buttons doesn't warrant multiple subcomponents
- Pattern matches existing `Users.razor` component structure

**2. Service Layer Reuse**
- Will use existing `UserManagementService` for database access
- New method: `ApproveUserAsync(Guid userId, Guid approvedByAdminId)` to set `ApprovedAt` timestamp and `ApprovedBy` foreign key
- Rationale: Maintains consistency with existing user management patterns

**3. Pure Blazor Confirmation**
- Component-level state-based confirmation (no JavaScript interop)
- Confirmation message displayed inline when user clicks approve
- "Confirm" and "Cancel" buttons replace the approve button temporarily
- Rationale: Pure Blazor approach, no JS dependencies, better testability

**4. Database Schema Addition**
- Add `ApprovedBy` (Guid?) foreign key to `ApplicationUser` model
- References `users` table (the admin who approved the account)
- Enables direct querying of approval history without parsing logs

**5. Independent Component**
- Does NOT integrate with existing `Dashboard.razor` or `Users.razor`
- Can be embedded in admin dashboard separately
- Rationale: Per requirements, should function independently

---

## Data Flow

### User Query Logic
```csharp
// Filter for pending approval accounts
// Only admins and clinicians require approval - patients are auto-approved on registration
var pendingUsers = await UserManagementService.GetAllUsersAsync();
pendingUsers = pendingUsers
    .Where(u => u.ApprovedAt == null
             && u.DeactivatedAt == null
             && (u.Type == "Admin" || u.Type == "Clinician"))
    .ToList();
```

**Note:**
- Patient accounts should have `ApprovedAt` set automatically during registration
- This filter ensures only admin/clinician accounts (which require approval) are displayed
- Verify that `ApplicationUser.Type` values in database match case exactly ("Admin"/"Clinician" vs "admin"/"clinician")

### Approval Process
1. Admin clicks "Approve" button next to user
2. Component state changes: store `pendingApprovalUserId` and show inline confirmation
3. Approve button replaced with "Confirm" (green) and "Cancel" (gray) buttons
4. Confirmation message: "Approve [User Name]'s account?"
5. If "Confirm" clicked:
   - Get current admin's user ID from authentication context
   - Call `UserManagementService.ApproveUserAsync(userId, currentAdminId)`
   - Service method sets `ApprovedAt = DateTime.UtcNow` and `ApprovedBy = currentAdminId`
   - Call `UserManager.UpdateAsync()` to persist
   - Reload pending users list (approved user removed from view)
   - Show success feedback
6. If "Cancel" clicked:
   - Clear `pendingApprovalUserId` state
   - Restore normal approve button

---

## Database Schema Interaction

### ApplicationUser Model
```csharp
public class ApplicationUser : IdentityUser<Guid>
{
    // Existing fields...
    public DateTime? ApprovedAt { get; set; }    // NULL = pending approval
    public Guid? ApprovedBy { get; set; }        // Foreign key to admin who approved
    public DateTime? DeactivatedAt { get; set; }  // NULL = active

    // Navigation property
    public virtual ApplicationUser? ApprovedByAdmin { get; set; }
}
```

**Migration Required:** Add `ApprovedBy` column to `users` table with foreign key constraint referencing `users.user_id`.

### Service Method (to be added)
```csharp
public async Task<(bool Success, string Message)> ApproveUserAsync(Guid userId, Guid approvedByAdminId)
{
    var user = await _userManager.FindByIdAsync(userId.ToString());
    if (user == null)
        return (false, "User not found");

    // Idempotent check - prevents race condition if multiple admins approve simultaneously
    if (user.ApprovedAt != null)
    {
        _logger.LogWarning("Attempted to approve already-approved user {UserId}", userId);
        return (false, "User already approved");
    }

    // Set approval timestamp and approving admin
    user.ApprovedAt = DateTime.UtcNow;
    user.ApprovedBy = approvedByAdminId;

    var result = await _userManager.UpdateAsync(user);

    if (!result.Succeeded)
    {
        var errors = string.Join(", ", result.Errors.Select(e => e.Description));
        _logger.LogError("Failed to approve user {UserId}: {Errors}", userId, errors);
        return (false, $"Failed to approve user: {errors}");
    }

    // Audit logging for compliance and tracking
    _logger.LogInformation(
        "User {UserId} ({UserEmail}) approved by admin {AdminId} at {Timestamp}",
        userId, user.Email, approvedByAdminId, user.ApprovedAt);

    return (true, "User approved successfully");
}
```

**Race Condition Protection:** The `ApprovedAt != null` check makes this method idempotent. If two admins approve simultaneously, second approval will fail gracefully with "already approved" message.

**Note:** `approvedByAdminId` is now required (not optional) to ensure approval tracking.

---

## UI Design

### Table Columns
1. **Name** - `FullName` property (FirstName + LastName)
2. **Email** - User's email address
3. **Type** - User type badge (same styling as Users.razor)
   - Only displays "Admin" and "Clinician" (patients auto-approved, never shown here)
4. **Requested** - `CreatedAt` timestamp (when they registered)
5. **Actions** - Approve button

### Visual Design
- Follows existing admin dashboard styling
- Color-coded user type badges:
  - Admin: Black (#030213)
  - Clinician: Green (#10b981)
- **Approve button:** Primary green color (success action)
- **Confirmation state:** When approve clicked:
  - Show inline message: "Approve [User Name]'s account?"
  - "Confirm" button (green, prominent)
  - "Cancel" button (gray, secondary)
  - Original approve button hidden
- Empty state message: "No pending approvals" (muted text)
- Loading state: "Loading pending approvals..."
- Pending count badge in header: Shows "X pending approval(s)" for visibility

### Responsive Behavior
- Same table structure as `Users.razor`
- Scrollable container for many pending users
- Action buttons remain accessible on all screen sizes

---

## Implementation Steps

### Phase 0: Database Schema Update
1. **Create migration for `ApprovedBy` field**
   - Add `ApprovedBy` (Guid?) column to `users` table
   - Add foreign key constraint: `FOREIGN KEY (ApprovedBy) REFERENCES users(user_id)`
   - Add index on `ApprovedBy` for query performance
2. **Update `ApplicationUser` model**
   - Add `ApprovedBy` property
   - Add `ApprovedByAdmin` navigation property
3. **Apply migration to database**

### Phase 0.5: Pre-Implementation Verification
1. Verify `ApplicationUser.Type` field values in database
   - Check case sensitivity: "Admin"/"Clinician" vs "admin"/"clinician"
   - Update filter logic in component if needed
2. Confirm `ApprovedAt` migration has been applied to database
   - Per commit 4175998: "Added approvedAt field to user accounts"
3. **Verify patient registration auto-approves accounts**
   - Check patient registration logic sets `ApprovedAt` immediately
   - Patients should never have `ApprovedAt = NULL`
4. Verify `[Authorize(Roles = "Admin")]` role name matches database exactly

### Phase 1: Service Layer Extension
1. Add `ApproveUserAsync(Guid userId, Guid approvedByAdminId)` method to `UserManagementService.cs`
   - Note: `approvedByAdminId` is required parameter
2. Inject `ILogger<UserManagementService>` for audit logging (if not already injected)
3. Set both `ApprovedAt` and `ApprovedBy` fields
4. Handle edge cases:
   - User not found
   - User already approved (idempotent)
   - Database update failures
5. Add audit logging for compliance tracking

### Phase 2: Component Creation
1. Create `PendingApprovals.razor` component
2. Implement component structure:
   - Page directive (if standalone route)
   - InteractiveServer render mode
   - Admin role authorization
3. Add service injections:
   - `UserManagementService`
   - `AuthenticationStateProvider` (to get current admin ID)
4. Add state variables:
   - `List<ApplicationUser> pendingUsers`
   - `Guid? pendingApprovalUserId` (for tracking which user is awaiting confirmation)
   - `bool isLoading`
   - `string? errorMessage`
5. Implement `OnInitializedAsync()` lifecycle method

### Phase 3: UI Implementation
1. Create table structure for pending users
2. Add loading and empty states
3. Add pending count badge in component header
4. **Implement pure Blazor confirmation workflow:**
   - "Approve" button click: sets `pendingApprovalUserId` state
   - Conditional rendering: if `pendingApprovalUserId == user.Id`:
     - Show confirmation message with user name
     - Show "Confirm" button (calls approval method with current admin ID)
     - Show "Cancel" button (clears `pendingApprovalUserId`)
   - Else: show normal "Approve" button
5. Retrieve current admin ID from `AuthenticationStateProvider`
6. Pass current admin ID to `ApproveUserAsync()` method
7. Add success/error feedback mechanism (consider toast notifications)
8. Style with scoped CSS (confirmation state styling)
9. Filter users by Type (Admin/Clinician only - exclude patients)

### Phase 4: Testing Checklist
- [ ] **Database migration applied:** `ApprovedBy` column exists with foreign key
- [ ] **Patient registration auto-approval verified:** Patients have `ApprovedAt` set on creation
- [ ] Displays only users with `ApprovedAt == null`
- [ ] Excludes deactivated users (`DeactivatedAt != null`)
- [ ] **Only shows admin/clinician accounts** (patients never appear due to auto-approval)
- [ ] Shows correct user types (admin, clinician)
- [ ] Pending count displays correct number
- [ ] **Pure Blazor confirmation:** Approve button shows inline confirm/cancel (no JavaScript)
- [ ] Cancel button restores normal approve button state
- [ ] Approval sets both `ApprovedAt` timestamp AND `ApprovedBy` foreign key
- [ ] `ApprovedBy` correctly references the current admin's user ID
- [ ] User disappears from list after approval
- [ ] **Race condition handling:** Multiple admins approving same user simultaneously
- [ ] **Audit logging:** Approval actions logged with admin ID and timestamp
- [ ] Error handling for database failures
- [ ] Loading state displays during data fetch
- [ ] Empty state shows when no pending approvals

---

## Security Considerations

1. **Authorization**
   - Component restricted to Admin role only via `[Authorize(Roles = "Admin")]`
   - **VERIFY:** Role name must match database exactly (case-sensitive)
   - Matches pattern from `Users.razor`

2. **Input Validation**
   - User ID validated by service layer (Guid type safety)
   - Service checks user exists before updating
   - Idempotent operation prevents duplicate approvals

3. **Audit Trail**
   - `ApprovedAt` timestamp records when approval occurred
   - **`ApprovedBy` foreign key** directly tracks which admin performed action in database
   - All approval actions logged via `ILogger` for compliance
   - Log entries include: user ID, email, admin ID, timestamp
   - Dual tracking: database foreign key + log entries for redundancy

4. **Race Condition Protection**
   - Idempotent `ApprovedAt != null` check prevents duplicate processing
   - Multiple simultaneous approvals handled gracefully
   - Second approval returns "already approved" message instead of error

---

## Future Enhancements

1. **Rejection Workflow**
   - Add "Reject" button to set `DeactivatedAt` without approval
   - Prevents rejected users from appearing in pending list

2. **Bulk Approval**
   - Checkboxes to select multiple users
   - "Approve Selected" button for batch operations

3. **Filtering/Search**
   - Search by name or email
   - Filter by user type (admin vs clinician)

4. **Real-time Updates**
   - SignalR integration to update pending list when other admins approve users
   - Prevents stale data in multi-admin scenarios

5. **Email Notifications**
   - Send email to user when account approved
   - Integration with email service

6. **Approval History View**
   - Query `ApprovedBy` navigation property to show admin details
   - Display "Approved by [Admin Name] on [Date]" in user management views

---

## Integration Points

### Admin Dashboard Embedding
The component can be embedded in the main admin dashboard as a widget or accessed as a standalone page:

**Option A: Standalone Route**
```razor
@page "/admin/approvals"
```

**Option B: Dashboard Widget**
```razor
<!-- In Dashboard.razor -->
<PendingApprovals />
```

### Navigation
Add navigation link in `AdminHeader` component:
```html
<a href="/admin/approvals">Pending Approvals</a>
```

Or add as tab in admin dashboard navigation.

---

## Code Style Guidelines

1. **Documentation Standards**
   - Follow existing pattern: extensive XML comments
   - Inline code comments explaining "how" and "why"
   - Header comment blocks for each section

2. **Naming Conventions**
   - Component: `PendingApprovals.razor`
   - Service method: `ApproveUserAsync()`
   - CSS classes: BEM-style with `approval-` prefix

3. **Error Handling**
   - Try-catch in service layer
   - Return tuples: `(bool Success, string Message)`
   - Display error messages inline (no modals)

4. **State Management**
   - Component-level state (no global state)
   - Boolean flags for loading/success states
   - Clear state on component initialization

---

## Dependencies

- **Services:** `UserManagementService` (existing)
- **Models:** `ApplicationUser` (existing - with `ApprovedAt` field per commit 4175998)
- **Layout:** `AdminHeader` (existing - if using)
- **Authorization:** ASP.NET Core Identity (existing)
- **Authentication:** `AuthenticationStateProvider` for retrieving current admin ID
- **Logging:** `ILogger<UserManagementService>` for audit trail
- **Database:** Migration required for `ApprovedBy` foreign key column

---

## Success Criteria

✅ **Database schema updated:** `ApprovedBy` column exists with foreign key constraint
✅ **Patient registration auto-approves:** Patients never require manual approval
✅ Component loads and displays pending approval accounts
✅ **Only shows admin/clinician accounts** (patients auto-approved on registration)
✅ **Pure Blazor confirmation workflow** (no JavaScript interop)
✅ Approve button sets both `ApprovedAt` timestamp AND `ApprovedBy` foreign key
✅ `ApprovedBy` correctly references the approving admin's user ID
✅ User removed from list after approval
✅ Confirmation prevents accidental approvals (inline confirm/cancel buttons)
✅ **Race condition protection** (idempotent approval handling)
✅ **Audit logging** (all approvals logged with admin ID)
✅ Error handling with user-friendly messages
✅ Pending count badge displays in header
✅ Matches existing admin dashboard styling
✅ Authorized to admin role only
✅ Functions independently of other components

---

## References

- **Existing Code:** `Components/Pages/Admin/Users.razor`
- **Model:** `Models/ApplicationUser.cs`
- **Service:** `Services/UserManagementService.cs`
- **Requirements:** User Story 33 (account approval workflow)
