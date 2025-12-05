# GrapheneTrace User Stories

> **Progress Tracking**: Use checkboxes to mark completed stories. Keep this document updated as features are implemented.

---

## 📋 Story Assignments

### Thomas

#### Login & Authentication
- [X] **Story #1**: As a user, I want to have an account I can login to so I can access the features and data that are relevant to me without unauthorised people having access to my data.
- [X] **Story #33**: As a clinician, I want to create a clinician account that an admin can approve to be able to use the app.
- [X] **Story #34**: As a user, I want to stay logged in across browser sessions so I don't have to re-authenticate constantly.
- [X] **Story #35**: As a user, I want to log out securely so that others using my device cannot access my account.
- [X] **Story #36**: As a user, I want my session to automatically expire after a period of inactivity so my account remains secure if I forget to log out.

#### Pressure Data & Notifications
- [ ] **Story #7**: As a clinician, I want to be notified when patients are over their peak pressure index to know to check for signs of pressure ulcers when I next see them.
  - *Partial: AlertHub sends pressure alerts, but clinician dashboard doesn't yet subscribe to receive real-time notifications*
- [X] **Story #10**: As a patient, I want to get an alert when the peak pressure index is over the threshold I've set so that I can take immediate action to avoid developing pressure ulcers.
  - *Implemented: Browser notifications via NotificationService when thresholds are breached*
- [X] **Story #18**: As a patient, I want to receive alerts if there is an issue with the pressure sensor or if the equipment is faulty in any way.
  - *Note: Combined with "As a patient, I want to receive an alert if my device disconnects, turns off, or otherwise fails" and "As a patient, I want to receive alerts if there is any issue with pressure sensors"*
  - *Implemented: Equipment fault detection in AlertService with browser notifications for DeadPixels, Saturation, CalibrationDrift, etc.*
- [ ] **Story #28**: As a clinician, I want to monitor my patients pressure readings so that I can send direct notifications to my patients regarding using the equipment properly.
  - *Partial: ChatWindow enables messaging to patients; clinicians can view historical data via Patients page; live monitoring not yet implemented*

#### User Settings & Metrics
- [X] **Story #9**: As a patient, I want to set and change my (low and?) high pressure threshold so that I can adjust it for alerts as I see fit and adapt it for my needs.
- [X] **Story #37**: As a patient, I want to see my Peak Pressure Index displayed clearly on my dashboard so I can monitor my risk level.
  - *Implemented: Peak Pressure displayed in real-time on Patient Dashboard metrics strip with color coding*
- [X] **Story #38**: As a clinician, I want the app to automatically identify and highlight high-pressure regions in patient data so I can quickly spot areas of concern.
  - *Implemented: CalculatePeakPressureIndex uses flood-fill algorithm to identify connected pressure regions ≥10 pixels; heatmap visualization highlights regions*

#### Legal & Compliance
- [X] **Story #21**: As a patient, I want to view a privacy policy so I can understand how my data will be used and consent to it so I can be treated and monitored.

#### Patient-Clinician Assignment (moved from Andrei)
- [X] **Story #15**: As an admin, I want to add patients to a clinician's list to keep the correct people accessing the correct data.
  - *Implemented: Admin Users page has patient assignment modal with checkbox toggles*

---

### Andrei

#### Account Management
- [X] **Story #2**: As an admin, I want to create a clinician account that can access certain patient data so that they can track how their patients are doing.
- [X] **Story #3**: As an admin, I want to create a patient account that can access only their own summarised data so patient data is kept to the specific patient.
- [ ] **Story #A**: As a patient, I want to create an account to be able to use the app.
  - *Note: Initially admin creates all accounts while in early release medical equipment. Need to clarify if patients should be able to self-register.*
- [ ] **Story #16**: As a patient, I want to request updates to my personal information so that I can be addressed correctly when using the system.
- [ ] **Story #23**: As a patient, I want to update my personal details such as my mailing address so communication can be addressed to me properly.
- [X] **Story #4**: As an admin, I want to be able to add/update patient and clinician personal information to keep it accurate and up-to-date.
- [X] **Story #5**: As an admin, I want to delete clinician accounts if they are no longer practising/working at the company to keep patient records available only to those that should have access to them.
- [ ] **Story #6**: As a clinician, I want to request to add patients to my list of patients so I can access their information and data to treat them better.
  - *Note: Combined with Story #19 "As a clinician, I want to request access to a patient's past data so I can use it to better treat them"*
- [ ] **Story #20**: As an admin, I want to approve or deny a clinician's request for a new patient/patient data based on if that clinician should have access to it.

---

### Rachel

#### Pressure Data & Analysis
- [ ] **Story #8**: As a clinician, I want access to patient's data for further, detailed analysis when they have been over their peak pressure.
- [ ] **Story #22**: As a clinician, I want to request access to a device's maintenance records and past readings so I can better judge if unusual readings are real or the result of a hardware issue.
- [ ] **Story #24**: As a clinician, I want to download/export a patient's data report so that I can share it with other healthcare professionals or attach it to medical records.
- [ ] **Story #25**: As a clinician, I want to filter patient data by date and time range so that I can focus only on relevant sessions.
- [ ] **Story #27**: As a clinician, I want to flag certain high-risk sessions in the database so that I can review them in more detail later.
  - *Partial: Sessions are auto-flagged when threshold breaches occur; manual flagging by clinicians not yet implemented*

#### Pressure Data - Graphs & Visualization
- [ ] **Story #11**: As a patient, I want to have the data shown in a pressure map time graph, where I can choose the time period displayed, to clearly show me where my highest pressure so I can decide whether I want to take action.
- [ ] **Story #12**: As a patient, I want to view graphs of key metrics to help with pattern spotting and decision making for my health.

#### Pressure Data - Summary
- [ ] **Story #26**: As a patient, I want the app to summarise my weekly trends in plain language (e.g. "pressure was higher than usual 3 times this week") so that I can easily understand the data.
  - *Partial: Weekly Summary card shows session count and alert comparisons (e.g., "3 monitoring sessions this week. No pressure alerts - great job!")*

---

### Awais

#### Account Management / User Data
- [ ] **Story #32**: As an admin, I want to have a list of users who are inactive for certain period of time so that I can check if they still want the account or not.

#### Pressure Data / Notifications
- [ ] **Story #17**: As a clinician, if I am assigned to certain patients, I want to be able to monitor their pressure readings so that I can send direct notifications to them (patients) about using the equipment properly.

#### Comments & Communication
- [ ] **Story #13**: As a patient, I want to add comments to the pressure map at times when I was aware of key information that isn't recorded by the sensor to help explain readings or add extra info for my clinician.
- [ ] **Story #14**: As a clinician, I want to view and reply to patients comments on their pressure map to help in my analysis and give them extra information they need quickly.
- [ ] **Story #30**: As a clinician, I want to request request access to a patient's medical history so that I can advise them accordingly.

#### Live Chat
- [ ] **Story #31**: As a patient, I want to have a live chat option so that I can seek direct help if I am facing any issues.

---

## 🚫 Out of Scope

These stories are explicitly marked as out of scope for the current implementation:

- **Story #E**: As a patient, I want there to be data security in place (beyond logins) so that only myself and my clinician can access my data.
  - *Reason: Out of scope unless someone wants to do it as an extra feature*

- **Story #B**: As a patient, I want to connect my device to start using it with the app.
  - *Reason: Our version doesn't connect to a real device, uses mock test data*

- **Story #C**: As a patient, I want to calibrate and recalibrate my device to get more accurate measurements.
  - *Reason: Our version won't be connecting to the device*

---

## 📊 Progress Summary

| Developer | Total Stories | Completed | Remaining |
|-----------|---------------|-----------|-----------|
| Thomas    | 14            | 12        | 2         |
| Andrei    | 10            | 4         | 6         |
| Rachel    | 8             | 0         | 8         |
| Awais     | 6             | 0         | 6         |
| **Total** | **38**        | **16**    | **22**    |

*Note: Stories #7 and #28 (Thomas), #26 and #27 (Rachel) have partial implementations noted above.*

---

## 📝 Notes for Contributors

- **Update checkboxes** as you complete stories
- **Mark completion dates** in commit messages when closing a story
- **Reference story numbers** in PR descriptions (e.g., "Implements Story #1")
- **Ask questions** if story requirements are unclear - better to clarify before coding
- **Break down large stories** into smaller tasks if needed
- **Update the progress summary table** after completing stories

---

*Last updated: 2025-12-05*
