# Patient Pressure Threshold Settings - Testing Checklist

**Author:** SID:2412494
**Created:** 2025-11-10
**Component:** `/patient/settings` (Index.razor)
**Story:** #9 - Patient pressure threshold configuration
**Purpose:** Manual testing guide for patient settings workflow

---

## Prerequisites: Start the Application

```bash
# From repository root
cd web-implementation/
dotnet watch run
```

Then navigate to `https://localhost:5001` (or the port shown in terminal)

---

## Test Setup: Create a Patient Account

**Option 1: Use existing patient account**
- If you already have a patient account, skip to Test Cases

**Option 2: Create new patient account**
1. Navigate to `/account-creation`
2. Fill in:
   - First Name: `Test`
   - Last Name: `Patient`
   - Email: `testpatient@example.com`
   - Password: `Password123!`
   - Account Type: **Patient**
3. Complete registration
4. Login with these credentials

---

## Test Cases

### ✅ Test 1: Initial Settings Load - Auto-Creation

**Goal**: Verify default settings are created for new patients

**Steps:**
1. Log in as a patient account (newly created)
2. Navigate to `/patient/settings`
3. Wait for page to load

**Expected Results:**
- [ ] Page loads without errors
- [ ] "Loading settings..." message appears briefly
- [ ] Default values displayed:
  - Low Pressure Threshold: **50**
  - High Pressure Threshold: **200**
- [ ] No error messages shown
- [ ] "Last updated" timestamp is displayed

**Database Verification:**
```sql
SELECT * FROM "PatientSettings"
WHERE "UserId" = (
    SELECT "Id" FROM "AspNetUsers"
    WHERE "Email" = 'testpatient@example.com'
);
```
Expected: One row with LowPressureThreshold=50, HighPressureThreshold=200

---

### ✅ Test 2: Valid Settings Update

**Goal**: Verify patients can update thresholds with valid values

**Steps:**
1. On `/patient/settings` page
2. Change Low Pressure Threshold to `60`
3. Change High Pressure Threshold to `180`
4. Click "Save Settings"
5. Wait for response

**Expected Results:**
- [ ] Button text changes to "Saving..." while processing
- [ ] Button is disabled during save
- [ ] Green success message appears: "Settings saved successfully!"
- [ ] Form values remain at 60 and 180
- [ ] "Last updated" timestamp updates
- [ ] No error messages shown

**Database Verification:**
```sql
SELECT "LowPressureThreshold", "HighPressureThreshold", "UpdatedAt"
FROM "PatientSettings"
WHERE "UserId" = (
    SELECT "Id" FROM "AspNetUsers"
    WHERE "Email" = 'testpatient@example.com'
);
```
Expected: LowPressureThreshold=60, HighPressureThreshold=180, UpdatedAt recent

---

### ✅ Test 3: Validation - Low >= High

**Goal**: Verify validation rejects low threshold greater than or equal to high threshold

**Steps:**
1. On `/patient/settings` page
2. Change Low Pressure Threshold to `150`
3. Change High Pressure Threshold to `150`
4. Click "Save Settings"

**Expected Results:**
- [ ] Red error message appears: "Low threshold must be less than high threshold."
- [ ] No success message shown
- [ ] Values remain in form (not reset)
- [ ] Database is NOT updated

**Repeat with Low > High:**
1. Change Low Pressure Threshold to `200`
2. Change High Pressure Threshold to `150`
3. Click "Save Settings"

**Expected Results:**
- [ ] Same error message appears
- [ ] Database is NOT updated

---

### ✅ Test 4: Validation - Low Threshold Out of Range

**Goal**: Verify validation enforces 1-254 range for low threshold

**Test 4a: Low threshold too low**
1. Change Low Pressure Threshold to `0`
2. Change High Pressure Threshold to `200`
3. Click "Save Settings"

**Expected Results:**
- [ ] Red error message appears (may be from client or server)
- [ ] Database is NOT updated

**Test 4b: Low threshold at minimum valid (boundary test)**
1. Change Low Pressure Threshold to `1`
2. Change High Pressure Threshold to `200`
3. Click "Save Settings"

**Expected Results:**
- [ ] Save succeeds
- [ ] Green success message appears
- [ ] Database updated to LowPressureThreshold=1

**Test 4c: Low threshold at maximum valid (boundary test)**
1. Change Low Pressure Threshold to `254`
2. Change High Pressure Threshold to `255`
3. Click "Save Settings"

**Expected Results:**
- [ ] Save succeeds
- [ ] Green success message appears
- [ ] Database updated to LowPressureThreshold=254

**Test 4d: Low threshold too high**
1. Change Low Pressure Threshold to `255`
2. Change High Pressure Threshold to `255`
3. Click "Save Settings"

**Expected Results:**
- [ ] Red error message appears
- [ ] Database is NOT updated

---

### ✅ Test 5: Validation - High Threshold Out of Range

**Goal**: Verify validation enforces 2-255 range for high threshold

**Test 5a: High threshold too low**
1. Change Low Pressure Threshold to `1`
2. Change High Pressure Threshold to `1`
3. Click "Save Settings"

**Expected Results:**
- [ ] Red error message appears
- [ ] Database is NOT updated

**Test 5b: High threshold at minimum valid (boundary test)**
1. Change Low Pressure Threshold to `1`
2. Change High Pressure Threshold to `2`
3. Click "Save Settings"

**Expected Results:**
- [ ] Save succeeds
- [ ] Green success message appears
- [ ] Database updated to HighPressureThreshold=2

**Test 5c: High threshold at maximum valid (boundary test)**
1. Change Low Pressure Threshold to `50`
2. Change High Pressure Threshold to `255`
3. Click "Save Settings"

**Expected Results:**
- [ ] Save succeeds
- [ ] Green success message appears
- [ ] Database updated to HighPressureThreshold=255

**Test 5d: High threshold too high**
1. Change Low Pressure Threshold to `50`
2. Change High Pressure Threshold to `256`
3. Click "Save Settings"

**Expected Results:**
- [ ] Red error message appears (may appear on input change)
- [ ] Database is NOT updated

---

### ✅ Test 6: Reset Button Functionality

**Goal**: Verify reset button reloads current saved values

**Steps:**
1. Change Low Pressure Threshold to `100`
2. Change High Pressure Threshold to `150`
3. **Do NOT click Save**
4. Click "Reset" button
5. Wait for reload

**Expected Results:**
- [ ] Form values revert to last saved values (e.g., 60 and 180 from Test 2)
- [ ] "Loading settings..." message appears briefly
- [ ] Any error/success messages are cleared
- [ ] No database changes occur

---

### ✅ Test 7: Settings Persistence Across Sessions

**Goal**: Verify settings persist after logout/login

**Steps:**
1. Update settings to unique values (e.g., Low=75, High=195)
2. Click "Save Settings"
3. Verify success message
4. Log out from patient account
5. Log back in as same patient
6. Navigate to `/patient/settings`

**Expected Results:**
- [ ] Form displays the values saved before logout (75 and 195)
- [ ] "Last updated" timestamp matches previous save
- [ ] No "default" values shown

---

### ✅ Test 8: Multiple Patients - Isolation

**Goal**: Verify settings are isolated per patient (not shared)

**Setup:**
1. Create a second patient account:
   - Email: `testpatient2@example.com`
   - Password: `Password123!`

**Steps:**
1. Log in as first patient (`testpatient@example.com`)
2. Navigate to `/patient/settings`
3. Set Low=80, High=220
4. Save settings
5. Log out
6. Log in as second patient (`testpatient2@example.com`)
7. Navigate to `/patient/settings`

**Expected Results:**
- [ ] Second patient sees default values (50, 200)
- [ ] Second patient does NOT see first patient's values (80, 220)
- [ ] Each patient has separate settings record in database

**Database Verification:**
```sql
SELECT
    u."Email",
    ps."LowPressureThreshold",
    ps."HighPressureThreshold"
FROM "PatientSettings" ps
JOIN "AspNetUsers" u ON ps."UserId" = u."Id"
WHERE u."Email" IN ('testpatient@example.com', 'testpatient2@example.com');
```
Expected: Two rows with different threshold values

---

### ✅ Test 9: Authorization - Non-Patient Access

**Goal**: Verify only patients can access settings page

**Test 9a: Clinician account**
1. Log out from patient account
2. Log in as a clinician account (or create one)
3. Attempt to navigate to `/patient/settings`

**Expected Results:**
- [ ] Redirected to `/access-denied` or login page
- [ ] Cannot access the settings page
- [ ] No error in browser console

**Test 9b: Admin account**
1. Log out from clinician account
2. Log in as admin account (or use `system@graphenetrace.local` / `System@Admin123`)
3. Attempt to navigate to `/patient/settings`

**Expected Results:**
- [ ] Redirected to `/access-denied` or login page
- [ ] Cannot access the settings page

**Test 9c: Unauthenticated user**
1. Log out completely
2. Navigate to `/patient/settings` directly

**Expected Results:**
- [ ] Redirected to `/login`
- [ ] Cannot access the settings page

---

### ✅ Test 10: Information Panel Display

**Goal**: Verify informational content is helpful

**Steps:**
1. Log in as patient
2. Navigate to `/patient/settings`
3. Scroll down to "Understanding Pressure Values" panel

**Expected Results:**
- [ ] Panel is visible below the form
- [ ] Shows correct information:
  - Sensor range: 1-255
  - Value 1: No pressure detected
  - Value 255: Maximum sensor capacity
  - Default low threshold: 50 (early warning)
  - Default high threshold: 200 (urgent alert)
- [ ] "Last updated" timestamp is displayed and accurate

---

### ✅ Test 11: Form Validation - Client Side

**Goal**: Verify HTML5 validation works before server submission

**Steps:**
1. Inspect the input fields in browser DevTools
2. Verify `min` and `max` attributes are present
3. Try entering values outside range directly in input

**Expected Results:**
- [ ] Low threshold input has `min="1"` and `max="254"`
- [ ] High threshold input has `min="2"` and `max="255"`
- [ ] Browser prevents typing values outside range (or shows validation message)
- [ ] Validation message appears inline (red text below input)

---

### ✅ Test 12: Navigation Links

**Goal**: Verify navigation to/from settings page works

**Steps:**
1. From `/patient/dashboard`, navigate to settings
2. From `/patient/settings`, click "← Back to Dashboard"

**Expected Results:**
- [ ] "Back to Dashboard" link is visible
- [ ] Clicking link navigates to `/patient/dashboard`
- [ ] No navigation errors or broken links

**Future Enhancement (when dashboard implemented):**
- [ ] Dashboard has "Manage Alert Settings" link
- [ ] Clicking dashboard link navigates to `/patient/settings`

---

### ✅ Test 13: Edge Cases - Rapid Updates

**Goal**: Verify system handles rapid successive saves

**Steps:**
1. Change settings to Low=70, High=190
2. Click "Save Settings"
3. **Immediately** change to Low=80, High=200
4. Click "Save Settings" again (quickly, before first save completes)

**Expected Results:**
- [ ] No JavaScript errors in console
- [ ] Button properly disables during each save
- [ ] Final saved values reflect the last submission
- [ ] No duplicate database records created
- [ ] UpdatedAt timestamp reflects the latest save

---

### ✅ Test 14: Error Handling - Database Unavailable

**Goal**: Verify graceful error handling when backend fails

**Setup:**
1. Stop the PostgreSQL database:
   ```bash
   docker compose stop postgres
   ```

**Steps:**
1. Navigate to `/patient/settings`
2. Try to load settings

**Expected Results:**
- [ ] Error message appears: "Error loading settings: ..."
- [ ] Page doesn't crash or show blank screen
- [ ] User is informed of the issue

**Cleanup:**
```bash
docker compose start postgres
```

---

### ✅ Test 15: Responsive Design

**Goal**: Verify mobile/tablet layouts work correctly

**Steps:**
1. Open browser DevTools (F12)
2. Enable responsive design mode
3. Test at different widths:
   - Mobile: 375px width
   - Tablet: 768px width
   - Desktop: 1280px width

**Expected Results:**
- [ ] Form remains readable at all widths
- [ ] Input fields don't overflow container
- [ ] Buttons stack appropriately on mobile
- [ ] Information panel remains readable
- [ ] No horizontal scrolling required
- [ ] Touch targets are large enough on mobile (min 44x44px)

---

### ✅ Test 16: Browser Compatibility

**Goal**: Verify component works across browsers

**Browsers to test:**
- [ ] Chrome/Chromium
- [ ] Firefox
- [ ] Safari (if available)
- [ ] Edge

**For each browser:**
1. Navigate to `/patient/settings`
2. Update settings and save
3. Verify success message appears

**Expected Results:**
- [ ] Component loads without errors
- [ ] Form validation works
- [ ] Save functionality works
- [ ] Styling is consistent

---

## Common Issues & Solutions

| Issue | Solution |
|-------|----------|
| 404 when navigating to `/patient/settings` | Verify migration applied and component compiled: `dotnet build` |
| "Cannot connect to database" error | Ensure PostgreSQL is running: `docker compose up -d` |
| No patient account to test with | Create one via `/account-creation` with Type: Patient |
| Settings don't save | Check browser console for errors, verify API endpoint `/api/settings` is accessible |
| 403 Forbidden on settings page | Ensure logged in as a patient (not admin/clinician) |
| Default values not appearing | Check PatientSettings table exists: `dotnet ef database update` |

---

## Quick Test Summary Checklist

**Core Functionality:**
- [ ] Default settings auto-created for new patients
- [ ] Valid settings updates succeed
- [ ] Settings persist across sessions
- [ ] Settings isolated per patient (not shared)
- [ ] Reset button reloads current values

**Validation:**
- [ ] Low threshold must be 1-254
- [ ] High threshold must be 2-255
- [ ] Low threshold must be less than high threshold
- [ ] Client-side validation works (HTML5)
- [ ] Server-side validation works (API)

**Authorization:**
- [ ] Only patients can access settings page
- [ ] Clinicians/admins redirected to access-denied
- [ ] Unauthenticated users redirected to login

**UI/UX:**
- [ ] Loading state displays during fetch
- [ ] Success/error messages display correctly
- [ ] Button disabled during save
- [ ] Information panel helpful and accurate
- [ ] Last updated timestamp accurate
- [ ] Responsive design works on mobile

**Data Integrity:**
- [ ] UpdatedAt timestamp updates on save
- [ ] No duplicate records created
- [ ] Boundary values (1, 2, 254, 255) handled correctly
- [ ] Database constraints enforced (unique UserId)

---

## Database Verification Queries

### View All Patient Settings
```sql
SELECT
    u."FirstName" || ' ' || u."LastName" AS "Patient",
    u."Email",
    ps."LowPressureThreshold",
    ps."HighPressureThreshold",
    ps."CreatedAt",
    ps."UpdatedAt"
FROM "PatientSettings" ps
JOIN "AspNetUsers" u ON ps."UserId" = u."Id"
WHERE u."UserType" = 'Patient'
ORDER BY ps."UpdatedAt" DESC;
```

### View Settings for Specific Patient
```sql
SELECT
    ps."LowPressureThreshold",
    ps."HighPressureThreshold",
    ps."CreatedAt",
    ps."UpdatedAt"
FROM "PatientSettings" ps
JOIN "AspNetUsers" u ON ps."UserId" = u."Id"
WHERE u."Email" = 'testpatient@example.com';
```

### Check for Duplicate Settings (Should return 0)
```sql
SELECT "UserId", COUNT(*)
FROM "PatientSettings"
GROUP BY "UserId"
HAVING COUNT(*) > 1;
```

### Manually Update Settings (Emergency)
```sql
UPDATE "PatientSettings"
SET "LowPressureThreshold" = 50,
    "HighPressureThreshold" = 200,
    "UpdatedAt" = NOW()
WHERE "UserId" = (
    SELECT "Id" FROM "AspNetUsers"
    WHERE "Email" = 'testpatient@example.com'
);
```

### Delete Settings to Test Auto-Creation
```sql
DELETE FROM "PatientSettings"
WHERE "UserId" = (
    SELECT "Id" FROM "AspNetUsers"
    WHERE "Email" = 'testpatient@example.com'
);
```

---

## API Testing (Optional - Using curl)

### GET Current Settings
```bash
# Get session cookie from browser DevTools > Application > Cookies
# Copy the .AspNetCore.Identity.Application cookie value

curl -X GET "https://localhost:5001/api/settings" \
  -H "Cookie: .AspNetCore.Identity.Application=YOUR_COOKIE_HERE" \
  -k
```

### PUT Update Settings
```bash
curl -X PUT "https://localhost:5001/api/settings" \
  -H "Content-Type: application/json" \
  -H "Cookie: .AspNetCore.Identity.Application=YOUR_COOKIE_HERE" \
  -d '{"lowThreshold": 75, "highThreshold": 195}' \
  -k
```

---

## Performance Testing (Optional)

### Load Time
1. Open browser DevTools > Network tab
2. Navigate to `/patient/settings`
3. Check "Finished" time in Network tab

**Expected:**
- [ ] Page loads in < 2 seconds on local development
- [ ] API call to `/api/settings` completes in < 500ms

### Database Query Performance
```sql
EXPLAIN ANALYZE
SELECT * FROM "PatientSettings"
WHERE "UserId" = 'test-uuid-here';
```

**Expected:**
- [ ] Query uses index on UserId (index scan, not seq scan)
- [ ] Execution time < 5ms

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

---

## Story Completion Criteria

Before marking Story #9 as complete, verify:
- [ ] All core functionality tests pass
- [ ] All validation tests pass
- [ ] Authorization tests pass
- [ ] Database integrity verified
- [ ] No console errors during normal usage
- [ ] Unit tests pass (11/11 in SettingsControllerTests)
- [ ] Code reviewed and approved
- [ ] Migration applied to database
- [ ] UserStories.md updated with checkmark
