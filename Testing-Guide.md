# GrapheneTrace - Complete Testing Guide

> **Purpose**: Comprehensive manual testing guide for all functionality in the GrapheneTrace application
> 
> **Last Updated**: 2025-01-XX
> 
> **Application Type**: Blazor Server Web Application for Pressure Ulcer Prevention

---

## Table of Contents

1. [Prerequisites & Setup](#prerequisites--setup)
2. [Authentication & Authorization](#authentication--authorization)
3. [Admin Features](#admin-features)
4. [Patient Features](#patient-features)
5. [Clinician Features](#clinician-features)
6. [Pressure Data & Heatmaps](#pressure-data--heatmaps)
7. [Comments System](#comments-system)
8. [Settings & Configuration](#settings--configuration)
9. [Integration Testing](#integration-testing)
10. [Edge Cases & Error Handling](#edge-cases--error-handling)

---

## Prerequisites & Setup

### Test Environment Setup

**Before Testing:**
1. Ensure Docker is running
2. Start PostgreSQL: `docker-compose up -d postgres`
3. Navigate to web-implementation: `cd web-implementation`
4. Apply migrations: `dotnet ef database update`
5. Run application: `dotnet watch run`
6. Open browser to: `https://localhost:5001`

### Test Accounts

All test accounts are seeded automatically on startup:

| Role | Email | Password | Status |
|------|-------|-----------|--------|
| **System Admin** | `system@graphenetrace.local` | `System@Admin123` | Auto-approved |
| **Patient (Alice)** | `patient.alice@graphenetrace.local` | `Patient@Alice123` | Auto-approved |
| **Patient (Bob)** | `patient.bob@graphenetrace.local` | `Patient@Bob123` | Auto-approved |
| **Patient (Carol)** | `patient.carol@graphenetrace.local` | `Patient@Carol123` | Auto-approved |
| **Patient (David)** | `patient.david@graphenetrace.local` | `Patient@David123` | Auto-approved |
| **Patient (Emma)** | `patient.emma@graphenetrace.local` | `Patient@Emma123` | Auto-approved |
| **Approved Clinician** | `clinician.approved@graphenetrace.local` | `Clinician@Approved123` | Approved |
| **Unapproved Clinician** | `clinician.pending@graphenetrace.local` | `Clinician@Pending123` | Pending |

### Browser Testing Setup

**Recommended:**
- Use Chrome/Edge for primary testing
- Use Firefox for cross-browser compatibility
- Test in incognito/private mode to avoid cookie conflicts
- Clear browser cache between test sessions

---

## Authentication & Authorization

### Test 1.1: User Login - Valid Credentials

**Steps:**
1. Navigate to `https://localhost:5001`
2. Enter email: `patient.alice@graphenetrace.local`
3. Enter password: `Patient@Alice123`
4. Click "Login"

**Expected Result:**
- ✅ Redirects to `/patient/dashboard`
- ✅ User's name appears in header
- ✅ Navigation menu shows patient-specific options
- ✅ No error messages displayed

**Positive Test Indicators:**
- Dashboard loads with patient data
- URL shows `/patient/dashboard`
- User is authenticated (can access protected pages)

---

### Test 1.2: User Login - Invalid Email

**Steps:**
1. Navigate to login page
2. Enter email: `nonexistent@test.com`
3. Enter password: `AnyPassword123!`
4. Click "Login"

**Expected Result:**
- ✅ Stays on login page
- ✅ Error message displayed: "Invalid email or password"
- ✅ No redirect occurs
- ✅ Password field is cleared (security best practice)

**Positive Test Indicators:**
- Error message is clear and user-friendly
- No sensitive information leaked in error
- User can retry login

---

### Test 1.3: User Login - Invalid Password

**Steps:**
1. Navigate to login page
2. Enter email: `patient.alice@graphenetrace.local`
3. Enter password: `WrongPassword123!`
4. Click "Login"

**Expected Result:**
- ✅ Stays on login page
- ✅ Error message: "Invalid email or password"
- ✅ Access failed count increments (check database)
- ✅ After 5 failed attempts, account locks for 15 minutes

**Positive Test Indicators:**
- Same error message as invalid email (prevents user enumeration)
- Lockout mechanism activates after threshold
- Security logging occurs

---

### Test 1.4: User Login - Unapproved Account

**Steps:**
1. Navigate to login page
2. Enter email: `clinician.pending@graphenetrace.local`
3. Enter password: `Clinician@Pending123`
4. Click "Login"

**Expected Result:**
- ✅ Stays on login page
- ✅ Error message: "Your account is pending administrator approval..."
- ✅ No redirect to dashboard
- ✅ User cannot access any protected pages

**Positive Test Indicators:**
- Clear messaging about approval status
- Security: Unapproved users cannot access system
- Admin can see pending approval in admin dashboard

---

### Test 1.5: User Login - Deactivated Account

**Steps:**
1. Admin deactivates a user account (via Admin > Users)
2. Attempt to login with deactivated account
3. Enter correct credentials

**Expected Result:**
- ✅ Login fails
- ✅ Error message: "This account has been deactivated. Please contact support."
- ✅ No access granted

**Positive Test Indicators:**
- Soft delete works correctly
- Deactivated users cannot authenticate
- Clear error messaging

---

### Test 1.6: Account Lockout

**Steps:**
1. Attempt login with wrong password 5 times
2. On 6th attempt, use correct password

**Expected Result:**
- ✅ First 5 attempts fail
- ✅ 6th attempt shows: "Account locked due to multiple failed login attempts. Please try again in 15 minutes."
- ✅ Cannot login even with correct password until lockout expires

**Positive Test Indicators:**
- Lockout activates at correct threshold (5 attempts)
- Lockout duration is 15 minutes
- Clear messaging about lockout status

---

### Test 1.7: Remember Me Functionality

**Steps:**
1. Login with "Remember Me" checkbox checked
2. Close browser completely
3. Reopen browser and navigate to application

**Expected Result:**
- ✅ User remains logged in
- ✅ Session persists across browser restarts
- ✅ Cookie expiration is extended

**Positive Test Indicators:**
- Persistent authentication cookie set
- User doesn't need to re-login
- Session security maintained

---

### Test 1.8: Session Timeout (20 Minutes)

**Steps:**
1. Login to application
2. Leave browser idle for 20+ minutes
3. Attempt to navigate to a page

**Expected Result:**
- ✅ Session expires after 20 minutes of inactivity
- ✅ Redirects to login page
- ✅ Error message: "Your session has expired. Please log in again."
- ✅ User must re-authenticate

**Positive Test Indicators:**
- Automatic session expiration works
- Security: Prevents unauthorized access if user forgets to logout
- Clear messaging about session expiration

---

### Test 1.9: User Logout

**Steps:**
1. Login to application
2. Click "Logout" button in header
3. Attempt to navigate to a protected page

**Expected Result:**
- ✅ Redirects to login page
- ✅ Authentication cookie cleared
- ✅ Cannot access protected pages
- ✅ Must re-authenticate to access system

**Positive Test Indicators:**
- Clean logout process
- No residual authentication state
- Security: Cannot access protected resources after logout

---

### Test 1.10: User Registration

**Steps:**
1. Navigate to registration page (if available)
2. Fill in form:
   - First Name: "Test"
   - Last Name: "User"
   - Email: `newuser@test.com`
   - Password: `NewUser@Password123`
   - Confirm Password: `NewUser@Password123`
   - User Type: "patient"
3. Submit form

**Expected Result:**
- ✅ Account created successfully
- ✅ Message: "Account created successfully! Your account is pending administrator approval..."
- ✅ Redirects to login page
- ✅ Cannot login until approved by admin

**Positive Test Indicators:**
- Account creation works
- Password validation enforced (12+ chars, uppercase, lowercase, number, special char)
- Approval workflow activated
- Email uniqueness enforced

---

### Test 1.11: Password Requirements Validation

**Steps:**
1. Attempt registration with weak passwords:
   - Too short: `Short1!`
   - No uppercase: `lowercase123!`
   - No lowercase: `UPPERCASE123!`
   - No number: `NoNumber!`
   - No special char: `NoSpecial123`

**Expected Result:**
- ✅ Each weak password is rejected
- ✅ Clear error message about missing requirement
- ✅ Account not created

**Positive Test Indicators:**
- All password requirements enforced
- Clear validation messages
- Security: Strong passwords required

---

### Test 1.12: Role-Based Access Control

**Steps:**
1. Login as Patient
2. Attempt to access `/admin/dashboard` directly via URL
3. Login as Clinician
4. Attempt to access `/admin/dashboard` directly via URL
5. Login as Admin
6. Access `/admin/dashboard`

**Expected Result:**
- ✅ Patient: Redirected to `/access-denied` or `/patient/dashboard`
- ✅ Clinician: Redirected to `/access-denied` or `/clinician/dashboard`
- ✅ Admin: Can access admin dashboard

**Positive Test Indicators:**
- Role-based routing works correctly
- Unauthorized access prevented
- Clear access denied messaging

---

## Admin Features

### Test 2.1: Admin Dashboard - Statistics Display

**Steps:**
1. Login as `system@graphenetrace.local`
2. Navigate to `/admin/dashboard`

**Expected Result:**
- ✅ Dashboard displays:
  - Total Users count
  - Clinicians count
  - Patients count
  - Pending Requests count
- ✅ "New Users" chart shows signup trends (last 30 days)
- ✅ "Recent Activity" section shows latest user creations
- ✅ All statistics are accurate

**Positive Test Indicators:**
- Statistics match database counts
- Chart displays correctly with data points
- Recent activity list shows user names and timestamps
- Page loads within 2 seconds

---

### Test 2.2: Admin - View All Users

**Steps:**
1. Login as admin
2. Navigate to `/admin/users`
3. Review user list

**Expected Result:**
- ✅ Table displays all users:
  - Name (First + Last)
  - Email
  - User Type (Admin/Clinician/Patient)
  - Status (Active/Deactivated)
  - Approval Status (Approved/Pending)
  - Created Date
- ✅ Users sorted by creation date (newest first)
- ✅ Search/filter functionality works (if implemented)

**Positive Test Indicators:**
- All seeded users visible
- User information accurate
- Table is sortable/filterable
- Pagination works (if many users)

---

### Test 2.3: Admin - Create New User

**Steps:**
1. Login as admin
2. Navigate to `/admin/users`
3. Click "Create User" or "Add User" button
4. Fill in form:
   - First Name: "John"
   - Last Name: "Doe"
   - Email: `john.doe@test.com`
   - User Type: "Patient"
   - Password: `JohnDoe@Password123`
5. Submit form

**Expected Result:**
- ✅ User created successfully
- ✅ Success message displayed
- ✅ User appears in users list
- ✅ User can login with provided credentials
- ✅ User is auto-approved (or requires approval based on type)

**Positive Test Indicators:**
- User creation form validates all fields
- Email uniqueness enforced
- Password requirements validated
- User appears immediately in list
- User can authenticate

---

### Test 2.4: Admin - Update User Information

**Steps:**
1. Login as admin
2. Navigate to `/admin/users`
3. Click "Edit" on a user
4. Update fields:
   - First Name: "Updated"
   - Last Name: "Name"
   - Email: `updated@test.com`
   - Phone: "123-456-7890"
   - Address: "123 Main St"
   - City: "London"
   - Postcode: "SW1A 1AA"
   - Country: "UK"
5. Save changes

**Expected Result:**
- ✅ User information updated successfully
- ✅ Success message displayed
- ✅ Updated information visible in user list
- ✅ Changes persist after page refresh

**Positive Test Indicators:**
- All fields update correctly
- Validation works (email format, etc.)
- Changes saved to database
- UI reflects changes immediately

---

### Test 2.5: Admin - Deactivate User (Soft Delete)

**Steps:**
1. Login as admin
2. Navigate to `/admin/users`
3. Click "Deactivate" or "Delete" on a user
4. Confirm deletion

**Expected Result:**
- ✅ User marked as deactivated (DeactivatedAt timestamp set)
- ✅ User disappears from active users list (or marked as inactive)
- ✅ User cannot login
- ✅ User data preserved in database (soft delete)

**Positive Test Indicators:**
- Soft delete works (data not permanently deleted)
- Deactivated users cannot authenticate
- Audit trail maintained
- Confirmation dialog prevents accidental deletion

---

### Test 2.6: Admin - Approve Pending Users

**Steps:**
1. Login as admin
2. Navigate to `/admin/approvals` or `/admin/pending-approvals`
3. View list of pending users
4. Click "Approve" on a pending clinician account
5. Confirm approval

**Expected Result:**
- ✅ User approved successfully
- ✅ ApprovedAt timestamp set
- ✅ ApprovedBy field set to current admin's ID
- ✅ User removed from pending list
- ✅ User can now login

**Positive Test Indicators:**
- Approval workflow works correctly
- Audit trail maintained (who approved, when)
- User can authenticate after approval
- Pending list updates immediately

---

### Test 2.7: Admin - Reject/Deny User Approval

**Steps:**
1. Login as admin
2. Navigate to pending approvals
3. Click "Reject" or "Deny" on a pending user
4. Confirm rejection

**Expected Result:**
- ✅ User remains unapproved
- ✅ User removed from pending list
- ✅ User cannot login
- ✅ Rejection logged (if implemented)

**Positive Test Indicators:**
- Rejection process works
- User status remains unchanged
- Clear messaging about rejection

---

### Test 2.8: Admin - Assign Patient to Clinician

**Steps:**
1. Login as admin
2. Navigate to `/admin/patient-clinician-assignment`
3. Select a patient from dropdown
4. Select a clinician from dropdown
5. Click "Assign"

**Expected Result:**
- ✅ Assignment created successfully
- ✅ Patient appears in clinician's patient list
- ✅ Clinician can access patient's data
- ✅ Assignment visible in assignments list

**Positive Test Indicators:**
- Assignment creates PatientClinician record
- Relationship persists in database
- Clinician can view patient data
- Duplicate assignments prevented

---

### Test 2.9: Admin - Unassign Patient from Clinician

**Steps:**
1. Login as admin
2. Navigate to patient-clinician assignments
3. Find an active assignment
4. Click "Unassign" or "Remove"
5. Confirm unassignment

**Expected Result:**
- ✅ Assignment soft-deleted (UnassignedAt timestamp set)
- ✅ Patient removed from clinician's patient list
- ✅ Clinician can no longer access patient's data
- ✅ Historical assignment data preserved

**Positive Test Indicators:**
- Soft delete maintains audit trail
- Relationship terminated correctly
- Access revoked immediately
- Historical data preserved

---

### Test 2.10: Admin - View Patient Requests

**Steps:**
1. Login as admin
2. Navigate to `/admin/patient-requests`
3. View list of patient-clinician requests

**Expected Result:**
- ✅ List displays:
  - Patient name
  - Clinician name
  - Request status (pending/approved/rejected)
  - Request date
  - Requested by (patient or clinician)
- ✅ Can approve or reject requests
- ✅ Approving creates assignment

**Positive Test Indicators:**
- All pending requests visible
- Request details accurate
- Approval/rejection workflow works
- Assignment created on approval

---

### Test 2.11: Admin - Change User Password (Admin Reset)

**Steps:**
1. Login as admin
2. Navigate to user management
3. Select a user
4. Click "Reset Password" or "Change Password"
5. Enter new password: `NewPassword@123`
6. Confirm password reset

**Expected Result:**
- ✅ Password reset successfully
- ✅ User can login with new password
- ✅ User cannot login with old password
- ✅ Password reset logged (audit trail)

**Positive Test Indicators:**
- Admin can reset any user's password
- New password works immediately
- Old password invalidated
- Security: Password reset requires admin privileges

---

## Patient Features

### Test 3.1: Patient Dashboard - Overview

**Steps:**
1. Login as `patient.alice@graphenetrace.local`
2. Navigate to `/patient/dashboard`

**Expected Result:**
- ✅ Dashboard displays:
  - Patient name and greeting
  - Recent pressure sessions
  - Key metrics (if implemented):
    - Peak Pressure Index
    - Contact Area %
    - Recent alerts
  - Quick links to:
    - Pressure Data
    - Sessions
    - Settings
    - Comments

**Positive Test Indicators:**
- Dashboard loads quickly (< 2 seconds)
- All sections display correctly
- Navigation links work
- Patient-specific data shown

---

### Test 3.2: Patient - View Pressure Sessions

**Steps:**
1. Login as patient
2. Navigate to `/patient/sessions`
3. View list of sessions

**Expected Result:**
- ✅ List displays all sessions for logged-in patient:
  - Session date/time
  - Device ID
  - Duration (if calculated)
  - Number of snapshots
  - Contact Area % (if available)
- ✅ Sessions sorted by date (newest first)
- ✅ Can click session to view details

**Positive Test Indicators:**
- Only patient's own sessions visible
- Sessions from CSV files loaded correctly
- Session data accurate
- Clicking session navigates to detail view

---

### Test 3.3: Patient - View Pressure Data/Heatmap

**Steps:**
1. Login as patient
2. Navigate to `/patient/pressure-data` or select a session
3. View heatmap visualization

**Expected Result:**
- ✅ Heatmap canvas displays
- ✅ Pressure data rendered as color-coded grid (32x32)
- ✅ Playback controls visible:
  - Play/Pause button
  - Frame slider/seek bar
  - Speed control
  - Render mode toggle (Discrete/Gradient)
- ✅ Current frame index displayed
- ✅ Total frames count displayed

**Positive Test Indicators:**
- Heatmap renders correctly
- Colors represent pressure values (blue=low, red=high)
- Playback works smoothly
- Controls are responsive
- Frame navigation works

---

### Test 3.4: Patient - Heatmap Playback Controls

**Steps:**
1. Navigate to pressure data page
2. Load a session with multiple frames
3. Test controls:
   - Click Play
   - Click Pause
   - Use slider to seek to different frame
   - Change playback speed
   - Toggle render mode

**Expected Result:**
- ✅ Play starts animation (15 FPS default)
- ✅ Pause stops animation
- ✅ Slider updates frame correctly
- ✅ Speed changes take effect
- ✅ Render mode switches between Discrete and Gradient
- ✅ Frame counter updates during playback

**Positive Test Indicators:**
- Playback smooth and responsive
- Controls work as expected
- No lag or stuttering
- Frame updates visible in real-time

---

### Test 3.5: Patient - Pressure Settings Configuration

**Steps:**
1. Login as patient
2. Navigate to `/patient/pressure-settings`
3. View current thresholds:
   - Low Pressure Threshold
   - High Pressure Threshold
4. Update thresholds:
   - Low: 50
   - High: 200
5. Save changes

**Expected Result:**
- ✅ Current settings displayed
- ✅ Default values shown if no settings exist
- ✅ Validation works:
   - Low threshold: 1-254
   - High threshold: 2-255
   - Low < High
- ✅ Settings saved successfully
- ✅ Success message displayed
- ✅ Updated settings persist after page refresh

**Positive Test Indicators:**
- Settings page loads with current values
- Validation prevents invalid inputs
- Settings saved to database
- Changes take effect immediately
- Default values from appsettings.json used

---

### Test 3.6: Patient - Add Comment to Pressure Data

**Steps:**
1. Login as patient
2. Navigate to `/patient/comments` or pressure data page
3. Click "Add Comment"
4. Enter comment text: "I was sitting in a wheelchair during this session"
5. Optionally link to specific session/frame
6. Submit comment

**Expected Result:**
- ✅ Comment created successfully
- ✅ Comment appears in comments list
- ✅ Comment timestamp displayed
- ✅ Comment linked to patient
- ✅ Clinician can view comment (if assigned)

**Positive Test Indicators:**
- Comment creation works
- Comments associated with correct patient
- Timestamps accurate
- Comments visible to assigned clinicians
- Session/frame linking works (if implemented)

---

### Test 3.7: Patient - View Own Comments

**Steps:**
1. Login as patient
2. Navigate to `/patient/comments`
3. View list of own comments

**Expected Result:**
- ✅ List displays:
  - Comment text
  - Timestamp
  - Linked session (if applicable)
  - Clinician reply (if exists)
- ✅ Comments sorted by date (newest first)
- ✅ Can view full comment details

**Positive Test Indicators:**
- Only patient's own comments visible
- Comments display correctly
- Replies from clinicians visible
- Navigation to linked sessions works

---

### Test 3.8: Patient - View Privacy Policy

**Steps:**
1. Login as patient
2. Navigate to privacy policy page (if available)
3. Or check footer/header for privacy policy link

**Expected Result:**
- ✅ Privacy policy page displays
- ✅ Content explains:
  - Data collection practices
  - Data usage
  - Data sharing
  - User rights
  - Contact information
- ✅ Policy is readable and comprehensive

**Positive Test Indicators:**
- Privacy policy accessible
- Content is clear and complete
- Legal compliance requirements met
- Link works from all pages

---

## Clinician Features

### Test 4.1: Clinician Dashboard - Overview

**Steps:**
1. Login as `clinician.approved@graphenetrace.local`
2. Navigate to `/clinician/dashboard`

**Expected Result:**
- ✅ Dashboard displays:
  - Clinician name and greeting
  - Assigned patients list
  - Recent patient activity
  - Alerts/notifications (if implemented)
  - Quick links to:
    - Patient List
    - Patient Details
    - Comments
    - Settings

**Positive Test Indicators:**
- Dashboard loads correctly
- Assigned patients visible
- Navigation links work
- Clinician-specific data shown

---

### Test 4.2: Clinician - View Patient List

**Steps:**
1. Login as clinician
2. Navigate to `/clinician/patient-list`
3. View assigned patients

**Expected Result:**
- ✅ List displays only patients assigned to this clinician:
  - Patient name
  - Email
  - Last session date
  - Status indicators
- ✅ Can click patient to view details
- ✅ Unassigned patients not visible

**Positive Test Indicators:**
- Only assigned patients visible
- Patient list accurate
- Clicking patient navigates to details
- Access control enforced (cannot see unassigned patients)

---

### Test 4.3: Clinician - View Patient Details

**Steps:**
1. Login as clinician
2. Navigate to patient list
3. Click on a patient name
4. View patient details page

**Expected Result:**
- ✅ Patient information displayed:
  - Name, email, contact info
  - Pressure sessions list
  - Recent pressure data
  - Comments from patient
  - Settings/thresholds
- ✅ Can navigate to specific sessions
- ✅ Can view heatmaps

**Positive Test Indicators:**
- Patient details accurate
- All patient data accessible
- Navigation works
- Data restricted to assigned patients only

---

### Test 4.4: Clinician - View Patient Pressure Data

**Steps:**
1. Login as clinician
2. Navigate to patient details
3. Select a pressure session
4. View heatmap visualization

**Expected Result:**
- ✅ Heatmap displays patient's pressure data
- ✅ Playback controls work
- ✅ Can analyze pressure patterns
- ✅ Can view comments on specific frames

**Positive Test Indicators:**
- Heatmap renders correctly
- Playback smooth
- Data accurate
- Comments visible on frames

---

### Test 4.5: Clinician - View Patient Comments

**Steps:**
1. Login as clinician
2. Navigate to `/clinician/comments`
3. View comments from assigned patients

**Expected Result:**
- ✅ List displays comments from all assigned patients:
  - Patient name
  - Comment text
  - Timestamp
  - Linked session/frame
  - Reply status (replied/not replied)
- ✅ Can filter by patient
- ✅ Can sort by date/patient

**Positive Test Indicators:**
- Only assigned patients' comments visible
- Comments display correctly
- Filtering/sorting works
- Can navigate to linked sessions

---

### Test 4.6: Clinician - Reply to Patient Comment

**Steps:**
1. Login as clinician
2. Navigate to comments page
3. Find a comment without reply
4. Click "Reply" or "Add Reply"
5. Enter reply text: "Thank you for the information. Please continue monitoring."
6. Submit reply

**Expected Result:**
- ✅ Reply created successfully
- ✅ Reply appears under comment
- ✅ Reply timestamp displayed
- ✅ Patient can view reply
- ✅ Comment marked as "replied"

**Positive Test Indicators:**
- Reply creation works
- Replies associated with correct comment
- Patient can see replies
- Reply status updates

---

### Test 4.7: Clinician - Request Patient Assignment

**Steps:**
1. Login as clinician
2. Navigate to patient list or request page
3. Click "Request Patient Access" or similar
4. Select a patient from list
5. Submit request

**Expected Result:**
- ✅ Request created successfully
- ✅ Request appears in admin's pending requests
- ✅ Request status: "pending"
- ✅ Admin can approve/reject request

**Positive Test Indicators:**
- Request creation works
- Request visible to admin
- Workflow follows approval process
- Duplicate requests prevented

---

### Test 4.8: Clinician - Cannot Access Unassigned Patients

**Steps:**
1. Login as clinician
2. Attempt to access patient details via direct URL:
   - `/clinician/patient-details?patientId={unassigned-patient-id}`
3. Or attempt to view unassigned patient's data

**Expected Result:**
- ✅ Access denied
- ✅ Redirected to appropriate page
- ✅ Error message displayed
- ✅ Cannot view patient data

**Positive Test Indicators:**
- Access control enforced
- Unassigned patients not accessible
- Security: Data isolation works correctly
- Clear error messaging

---

## Pressure Data & Heatmaps

### Test 5.1: Pressure Data Loading from CSV Files

**Steps:**
1. Ensure CSV files exist in `Resources/GTLB-Data/`
2. Start application (data loads on startup)
3. Check database for sessions

**Expected Result:**
- ✅ CSV files processed on startup
- ✅ Sessions created in database:
  - Device ID extracted from filename
  - Date extracted from filename
  - Patient assigned based on device ID mapping
- ✅ Snapshots created:
  - 32x32 grid per snapshot
  - Timestamps calculated (15 FPS)
  - Contact Area % calculated
- ✅ No duplicate sessions created on restart

**Positive Test Indicators:**
- All CSV files processed
- Sessions and snapshots in database
- Patient assignments correct
- No data duplication
- Processing completes without errors

---

### Test 5.2: Heatmap Rendering - Discrete Mode

**Steps:**
1. Navigate to pressure data page
2. Load a session
3. Set render mode to "Discrete"
4. View heatmap

**Expected Result:**
- ✅ Heatmap displays with 7 discrete color bands:
  - Blue (low pressure)
  - Cyan
  - Green
  - Yellow
  - Orange
  - Red (high pressure)
- ✅ Colors map correctly to pressure values
- ✅ Grid is 32x32 cells
- ✅ Each cell represents one sensor reading

**Positive Test Indicators:**
- Discrete mode renders correctly
- Color bands distinct and clear
- Pressure values mapped accurately
- Grid layout correct

---

### Test 5.3: Heatmap Rendering - Gradient Mode

**Steps:**
1. Navigate to pressure data page
2. Load a session
3. Set render mode to "Gradient"
4. View heatmap

**Expected Result:**
- ✅ Heatmap displays with smooth gradient colors
- ✅ Colors interpolate smoothly between pressure values
- ✅ Gradient transitions are smooth (no banding)
- ✅ High pressure areas clearly visible

**Positive Test Indicators:**
- Gradient mode renders correctly
- Smooth color transitions
- High pressure regions identifiable
- Performance acceptable (no lag)

---

### Test 5.4: Heatmap Playback - Frame Navigation

**Steps:**
1. Load a session with multiple frames
2. Use frame slider to navigate:
   - Jump to frame 0
   - Jump to middle frame
   - Jump to last frame
3. Verify frame updates

**Expected Result:**
- ✅ Slider updates frame correctly
- ✅ Heatmap re-renders for each frame
- ✅ Frame counter updates
- ✅ Can navigate to any frame instantly

**Positive Test Indicators:**
- Frame navigation responsive
- Heatmap updates immediately
- Frame counter accurate
- No lag or delay

---

### Test 5.5: Heatmap Playback - Speed Control

**Steps:**
1. Load a session
2. Start playback
3. Change playback speed:
   - 0.5x (slow)
   - 1x (normal)
   - 2x (fast)
   - 5x (very fast)

**Expected Result:**
- ✅ Playback speed changes take effect
- ✅ Animation speed adjusts correctly
- ✅ Frame updates at correct rate
- ✅ Can pause/resume at any speed

**Positive Test Indicators:**
- Speed control works
- Playback smooth at all speeds
- No frame drops or stuttering
- Speed changes apply immediately

---

### Test 5.6: Contact Area Calculation

**Steps:**
1. View a pressure session
2. Check Contact Area % value
3. Verify calculation accuracy

**Expected Result:**
- ✅ Contact Area % displayed for each snapshot
- ✅ Calculation: (sensors > 0) / 1024 * 100
- ✅ Values range from 0% to 100%
- ✅ Values accurate based on pressure data

**Positive Test Indicators:**
- Contact Area % calculated correctly
- Values make sense (not negative, not >100%)
- Calculation matches expected formula
- Displayed in appropriate format

---

### Test 5.7: Peak Pressure Index Calculation

**Steps:**
1. View pressure data
2. Check Peak Pressure Index (if displayed)
3. Verify calculation

**Expected Result:**
- ✅ Peak Pressure Index displayed
- ✅ Calculation excludes regions <10 pixels
- ✅ Value represents highest pressure region
- ✅ Updates during playback

**Positive Test Indicators:**
- Peak Pressure Index calculated correctly
- Small regions filtered out
- Value accurate
- Updates in real-time

---

## Comments System

### Test 6.1: Patient - Add General Comment

**Steps:**
1. Login as patient
2. Navigate to `/patient/comments`
3. Click "Add Comment"
4. Enter comment: "I noticed increased pressure during afternoon sessions"
5. Submit

**Expected Result:**
- ✅ Comment created successfully
- ✅ Comment appears in list
- ✅ Timestamp recorded
- ✅ Comment linked to patient
- ✅ Clinician can view (if assigned)

**Positive Test Indicators:**
- Comment creation works
- Comments saved to database
- Timestamps accurate
- Comments visible to assigned clinicians

---

### Test 6.2: Patient - Add Comment Linked to Session

**Steps:**
1. Login as patient
2. Navigate to a pressure session
3. Click "Add Comment" on specific frame
4. Enter comment: "I was repositioning at this time"
5. Submit

**Expected Result:**
- ✅ Comment created with SessionId
- ✅ Comment linked to specific frame (FrameIndex)
- ✅ Comment appears when viewing that session
- ✅ Clinician can see comment in context

**Positive Test Indicators:**
- Session linking works
- Frame linking works
- Comments appear in correct context
- Navigation to linked session works

---

### Test 6.3: Clinician - View All Patient Comments

**Steps:**
1. Login as clinician
2. Navigate to `/clinician/comments`
3. View comments list

**Expected Result:**
- ✅ List shows comments from all assigned patients
- ✅ Patient name displayed with each comment
- ✅ Comments sorted by date (newest first)
- ✅ Can filter by patient
- ✅ Can see reply status

**Positive Test Indicators:**
- All assigned patients' comments visible
- Patient names displayed correctly
- Filtering works
- Reply status accurate

---

### Test 6.4: Clinician - Reply to Comment

**Steps:**
1. Login as clinician
2. Navigate to comments page
3. Find comment without reply
4. Click "Reply"
5. Enter reply: "Thank you for the update. Please continue monitoring."
6. Submit

**Expected Result:**
- ✅ Reply saved successfully
- ✅ Reply appears under comment
- ✅ Reply timestamp recorded
- ✅ Comment marked as "replied"
- ✅ Patient can view reply

**Positive Test Indicators:**
- Reply creation works
- Replies linked to correct comment
- Timestamps accurate
- Patient can see replies
- Reply status updates

---

### Test 6.5: Comment Threading (if implemented)

**Steps:**
1. Patient adds comment
2. Clinician replies
3. Patient views comment and reply

**Expected Result:**
- ✅ Comments and replies displayed in thread
- ✅ Reply appears under original comment
- ✅ Threading structure clear
- ✅ Can navigate between comments and replies

**Positive Test Indicators:**
- Threading works correctly
- Visual hierarchy clear
- Navigation intuitive
- All replies visible

---

## Settings & Configuration

### Test 7.1: Patient Settings - View Current Thresholds

**Steps:**
1. Login as patient
2. Navigate to `/patient/pressure-settings`
3. View current settings

**Expected Result:**
- ✅ Current thresholds displayed:
  - Low Pressure Threshold
  - High Pressure Threshold
- ✅ Default values shown if no settings exist
- ✅ Last updated timestamp displayed
- ✅ Settings form is editable

**Positive Test Indicators:**
- Settings page loads correctly
- Current values displayed
- Defaults from appsettings.json used
- Form is functional

---

### Test 7.2: Patient Settings - Update Thresholds

**Steps:**
1. Navigate to pressure settings
2. Update thresholds:
   - Low: 75
   - High: 225
3. Save changes

**Expected Result:**
- ✅ Settings saved successfully
- ✅ Success message displayed
- ✅ Updated values persist
- ✅ Settings visible after page refresh
- ✅ UpdatedAt timestamp changes

**Positive Test Indicators:**
- Settings update works
- Changes saved to database
- Persistence confirmed
- Timestamps update

---

### Test 7.3: Patient Settings - Validation

**Steps:**
1. Navigate to pressure settings
2. Attempt invalid inputs:
   - Low threshold: 0 (below minimum)
   - Low threshold: 255 (above maximum)
   - High threshold: 1 (below minimum)
   - High threshold: 256 (above maximum)
   - Low = High (equal values)
   - Low > High (invalid relationship)

**Expected Result:**
- ✅ Each invalid input rejected
- ✅ Clear error message displayed
- ✅ Settings not saved
- ✅ Form shows validation errors

**Positive Test Indicators:**
- Validation works for all cases
- Error messages clear
- Invalid data not saved
- User can correct errors

---

### Test 7.4: Configuration - Pressure Thresholds from appsettings.json

**Steps:**
1. Check `appsettings.json` for PressureThresholds section
2. Verify configuration values:
   - MinValue: 1
   - MaxValue: 255
   - LowThresholdMin: 1
   - LowThresholdMax: 254
   - HighThresholdMin: 2
   - HighThresholdMax: 255
   - DefaultLowThreshold: 50
   - DefaultHighThreshold: 200
3. Verify validation uses these values

**Expected Result:**
- ✅ Configuration loaded at startup
- ✅ Validation uses configured ranges
- ✅ Defaults use configured values
- ✅ Invalid configuration caught at startup

**Positive Test Indicators:**
- Configuration system works
- Validation respects config
- Defaults from config
- Startup validation prevents runtime errors

---

## Integration Testing

### Test 8.1: End-to-End - Patient Workflow

**Steps:**
1. Login as patient
2. View dashboard
3. Navigate to sessions
4. Select a session
5. View heatmap
6. Add comment on a frame
7. Update pressure settings
8. View own comments
9. Logout

**Expected Result:**
- ✅ All steps complete successfully
- ✅ Data persists between steps
- ✅ Navigation works smoothly
- ✅ No errors encountered
- ✅ User experience is cohesive

**Positive Test Indicators:**
- Complete workflow functional
- Data consistency maintained
- Performance acceptable
- User can accomplish goals

---

### Test 8.2: End-to-End - Clinician Workflow

**Steps:**
1. Login as clinician
2. View dashboard
3. Navigate to patient list
4. Select a patient
5. View patient details
6. View patient's pressure data
7. View patient's comments
8. Reply to a comment
9. Logout

**Expected Result:**
- ✅ All steps complete successfully
- ✅ Can access assigned patients
- ✅ Cannot access unassigned patients
- ✅ Comments workflow works
- ✅ Data accurate

**Positive Test Indicators:**
- Complete workflow functional
- Access control enforced
- Comments system integrated
- Performance acceptable

---

### Test 8.3: End-to-End - Admin Workflow

**Steps:**
1. Login as admin
2. View dashboard statistics
3. Approve pending users
4. Create new user
5. Assign patient to clinician
6. View user list
7. Update user information
8. Logout

**Expected Result:**
- ✅ All admin functions work
- ✅ User management complete
- ✅ Approval workflow functional
- ✅ Assignments work correctly
- ✅ Statistics accurate

**Positive Test Indicators:**
- Complete admin workflow functional
- All management features work
- Data consistency maintained
- Performance acceptable

---

### Test 8.4: Cross-User Communication

**Steps:**
1. Patient adds comment
2. Clinician views comment
3. Clinician replies
4. Patient views reply
5. Verify communication flow

**Expected Result:**
- ✅ Patient comment visible to clinician
- ✅ Clinician reply visible to patient
- ✅ Comments linked correctly
- ✅ Timestamps accurate
- ✅ Communication works bidirectionally

**Positive Test Indicators:**
- Comment system enables communication
- Both users can see messages
- Data consistency maintained
- Real-time or near-real-time updates

---

## Edge Cases & Error Handling

### Test 9.1: Database Connection Failure

**Steps:**
1. Stop PostgreSQL container: `docker-compose down`
2. Attempt to access application
3. Try to login

**Expected Result:**
- ✅ Application handles connection failure gracefully
- ✅ Error message displayed (user-friendly)
- ✅ Application doesn't crash
- ✅ Can recover when database restarted

**Positive Test Indicators:**
- Graceful error handling
- User-friendly error messages
- Application stability
- Recovery possible

---

### Test 9.2: Invalid Session Data

**Steps:**
1. Manually insert invalid session data into database
2. Attempt to view session
3. Attempt to render heatmap

**Expected Result:**
- ✅ Application handles invalid data gracefully
- ✅ Error message displayed
- ✅ Doesn't crash
- ✅ Invalid data doesn't break rendering

**Positive Test Indicators:**
- Data validation works
- Error handling robust
- Application stability
- User experience maintained

---

### Test 9.3: Concurrent User Actions

**Steps:**
1. Login as two different users in different browsers
2. Perform simultaneous actions:
   - Both update settings
   - Both add comments
   - Admin approves while user logs in

**Expected Result:**
- ✅ No data corruption
- ✅ All actions complete successfully
- ✅ No race conditions
- ✅ Database consistency maintained

**Positive Test Indicators:**
- Concurrency handled correctly
- No data loss
- Transactions work correctly
- System stable under load

---

### Test 9.4: Large Dataset Performance

**Steps:**
1. Load session with many frames (1000+)
2. Navigate through frames
3. Playback entire session
4. Monitor performance

**Expected Result:**
- ✅ Application remains responsive
- ✅ Playback smooth
- ✅ No memory leaks
- ✅ Frame navigation works

**Positive Test Indicators:**
- Performance acceptable with large datasets
- No lag or stuttering
- Memory usage reasonable
- User experience maintained

---

### Test 9.5: Browser Compatibility

**Steps:**
1. Test in Chrome
2. Test in Firefox
3. Test in Safari (if on Mac)
4. Test in Edge

**Expected Result:**
- ✅ Application works in all browsers
- ✅ Heatmap renders correctly
- ✅ Playback controls work
- ✅ No browser-specific issues

**Positive Test Indicators:**
- Cross-browser compatibility
- Consistent functionality
- No browser-specific bugs
- User experience consistent

---

### Test 9.6: Network Interruption

**Steps:**
1. Start using application
2. Disconnect network (or stop server)
3. Attempt to perform actions
4. Reconnect network

**Expected Result:**
- ✅ Application handles disconnection gracefully
- ✅ Error messages displayed
- ✅ Can recover when reconnected
- ✅ No data loss

**Positive Test Indicators:**
- Network error handling works
- User experience maintained
- Recovery possible
- Data integrity preserved

---

### Test 9.7: Session Expiration During Use

**Steps:**
1. Login to application
2. Leave idle for 20+ minutes
3. Attempt to perform action

**Expected Result:**
- ✅ Session expires correctly
- ✅ Redirects to login
- ✅ Clear messaging
- ✅ Can re-authenticate

**Positive Test Indicators:**
- Session timeout works
- Security maintained
- User experience acceptable
- Re-authentication works

---

### Test 9.8: File Upload/Import Errors (if implemented)

**Steps:**
1. Attempt to upload invalid CSV file
2. Attempt to upload file with wrong format
3. Attempt to upload very large file

**Expected Result:**
- ✅ Invalid files rejected
- ✅ Clear error messages
- ✅ File size limits enforced
- ✅ No application crashes

**Positive Test Indicators:**
- File validation works
- Error handling robust
- User-friendly messages
- System stability maintained

---

## Performance Testing

### Test 10.1: Page Load Times

**Steps:**
1. Measure load time for each major page:
   - Login page
   - Dashboard (each role)
   - User list
   - Pressure data
   - Settings

**Expected Result:**
- ✅ All pages load within 2 seconds
- ✅ Dashboard loads within 1 second
- ✅ No significant delays

**Positive Test Indicators:**
- Acceptable load times
- Good user experience
- Performance meets requirements

---

### Test 10.2: Database Query Performance

**Steps:**
1. Monitor database queries during:
   - User list loading
   - Session list loading
   - Heatmap data loading
   - Comments loading

**Expected Result:**
- ✅ Queries execute quickly (< 500ms)
- ✅ No N+1 query problems
- ✅ Indexes used effectively

**Positive Test Indicators:**
- Query performance acceptable
- Database optimized
- No performance bottlenecks

---

### Test 10.3: Heatmap Rendering Performance

**Steps:**
1. Load session with many frames
2. Measure frame rendering time
3. Test playback performance

**Expected Result:**
- ✅ Frames render quickly (< 50ms)
- ✅ Playback smooth at 15 FPS
- ✅ No lag or stuttering

**Positive Test Indicators:**
- Rendering performance good
- Playback smooth
- User experience excellent

---

## Security Testing

### Test 11.1: SQL Injection Prevention

**Steps:**
1. Attempt SQL injection in:
   - Login form (email field)
   - Search fields
   - URL parameters

**Expected Result:**
- ✅ SQL injection attempts fail
- ✅ Parameterized queries used
- ✅ No database errors exposed
- ✅ Application remains secure

**Positive Test Indicators:**
- SQL injection prevented
- Security best practices followed
- No vulnerabilities exposed

---

### Test 11.2: XSS Prevention

**Steps:**
1. Attempt XSS in:
   - Comment fields
   - User input fields
   - URL parameters

**Expected Result:**
- ✅ XSS attempts sanitized
- ✅ Scripts don't execute
- ✅ HTML properly escaped
- ✅ Application remains secure

**Positive Test Indicators:**
- XSS prevented
- Input sanitization works
- Output encoding correct
- Security maintained

---

### Test 11.3: CSRF Protection

**Steps:**
1. Attempt to perform actions without proper tokens
2. Test form submissions
3. Test API calls

**Expected Result:**
- ✅ CSRF tokens required
- ✅ Invalid tokens rejected
- ✅ Protection active on all forms

**Positive Test Indicators:**
- CSRF protection active
- Tokens validated correctly
- Security maintained

---

### Test 11.4: Authorization Bypass Attempts

**Steps:**
1. Attempt to access admin pages as patient
2. Attempt to access other users' data
3. Attempt to modify other users' settings

**Expected Result:**
- ✅ All unauthorized access blocked
- ✅ Redirects to appropriate page
- ✅ Clear error messages
- ✅ Security maintained

**Positive Test Indicators:**
- Authorization enforced
- Access control works
- No privilege escalation
- Security maintained

---

## Regression Testing Checklist

After any code changes, verify these critical paths still work:

- [ ] User login/logout
- [ ] Admin user management
- [ ] Patient-clinician assignments
- [ ] Pressure data loading and display
- [ ] Heatmap rendering and playback
- [ ] Comments creation and replies
- [ ] Settings updates
- [ ] Session expiration
- [ ] Role-based access control
- [ ] Data persistence

---

## Test Reporting Template

For each test session, document:

**Test Date:** YYYY-MM-DD
**Tester:** Name
**Environment:** Development/Staging/Production
**Browser:** Chrome/Firefox/Safari/Edge
**Version:** Application version

**Tests Executed:**
- List of test IDs executed

**Results:**
- Pass: X tests
- Fail: Y tests
- Blocked: Z tests

**Issues Found:**
- Issue ID, Description, Severity, Steps to Reproduce

**Notes:**
- Any observations or recommendations

---

## Conclusion

This testing guide covers all major functionality in the GrapheneTrace application. Use this guide to:

1. **Verify functionality** after code changes
2. **Ensure quality** before releases
3. **Document test coverage** for stakeholders
4. **Identify issues** early in development
5. **Maintain application quality** over time

**Remember:**
- Test in a clean environment (fresh database)
- Use test accounts provided
- Document any issues found
- Verify fixes with regression testing
- Keep this guide updated as features change

---

*End of Testing Guide*

