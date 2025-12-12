using GrapheneTrace.Web.Data;
using GrapheneTrace.Web.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;

namespace GrapheneTrace.Web.Components.Pages.Clinician;

/// <summary>
/// Clinician Contact Patients Page Code-Behind
/// Author: 2402513
///
/// Purpose:
/// Manages the data loading and interaction logic for the Contact Patients page.
/// Loads assigned patients and provides mock message data for the UI mockup.
/// Handles patient selection and message display.
/// </summary>
public partial class Contact
{
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;
    [Inject] private ApplicationDbContext DbContext { get; set; } = default!;

    private bool isLoading = true;
    private Guid clinicianId = Guid.Empty;
    private List<PatientContactInfo>? patients;
    private Guid? selectedPatientId;
    private ApplicationUser? selectedPatient;
    private string newMessage = "";

    /// <summary>
    /// Patient contact information with mock message data.
    /// Author: 2402513
    /// </summary>
    private class PatientContactInfo
    {
        public Guid PatientId { get; set; }
        public ApplicationUser? Patient { get; set; }
        public string? MostRecentMessage { get; set; }
        public string? MostRecentMessageTime { get; set; }
    }

    /// <summary>
    /// Mock message data for UI display.
    /// Author: 2402513
    /// </summary>
    private class MockMessage
    {
        public string Text { get; set; } = "";
        public string Timestamp { get; set; } = "";
        public bool IsFromPatient { get; set; }
    }

    private List<MockMessage> mockMessages = new();

    protected override async Task OnInitializedAsync()
    {
        await LoadClinicianId();
        await LoadPatientData();
        isLoading = false;
    }

    /// <summary>
    /// Loads the clinician ID from the authenticated user.
    /// Author: 2402513
    /// </summary>
    private async Task LoadClinicianId()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;

        var clinicianIdClaim = user.FindFirst("sub")?.Value;
        if (Guid.TryParse(clinicianIdClaim, out var id))
        {
            clinicianId = id;
            return;
        }

        var username = user.Identity?.Name;
        if (!string.IsNullOrEmpty(username))
        {
            var clinician = await DbContext.Users
                .FirstOrDefaultAsync(u => u.UserName == username && u.UserType == "clinician");
            if (clinician != null)
            {
                clinicianId = clinician.Id;
                return;
            }
        }

        var email = user.FindFirst("email")?.Value ?? user.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
        if (!string.IsNullOrEmpty(email))
        {
            var clinician = await DbContext.Users
                .FirstOrDefaultAsync(u => u.Email == email && u.UserType == "clinician");
            if (clinician != null)
            {
                clinicianId = clinician.Id;
            }
        }
    }

    /// <summary>
    /// Loads all assigned patients with mock message data.
    /// Author: 2402513
    /// </summary>
    /// <remarks>
    /// This method loads patients and generates mock message data for the UI.
    /// In a real implementation, this would load actual messages from a database.
    /// </remarks>
    private async Task LoadPatientData()
    {
        if (clinicianId == Guid.Empty)
        {
            patients = new List<PatientContactInfo>();
            return;
        }

        // Load all assigned patients
        var patientClinicians = await DbContext.PatientClinicians
            .Where(pc => pc.ClinicianId == clinicianId && pc.UnassignedAt == null)
            .Include(pc => pc.Patient)
            .OrderBy(pc => pc.Patient!.LastName)
            .ThenBy(pc => pc.Patient!.FirstName)
            .ToListAsync();

        patients = new List<PatientContactInfo>();

        // Generate mock data for each patient
        var mockMessages = new[]
        {
            "Hello, how are you feeling today?",
            "I've been experiencing some discomfort.",
            "I noticed your pressure readings were elevated yesterday.",
            "Thank you for letting me know. I'll adjust my position more frequently.",
            "That's good. Keep monitoring and let me know if anything changes."
        };

        var random = new Random();

        foreach (var pc in patientClinicians)
        {
            if (pc.Patient == null || pc.PatientId == Guid.Empty)
                continue;

            var patientInfo = new PatientContactInfo
            {
                PatientId = pc.PatientId,
                Patient = pc.Patient
            };

            // Generate mock recent message for some patients
            if (random.Next(0, 2) == 0) // 50% chance of having a message
            {
                patientInfo.MostRecentMessage = mockMessages[random.Next(mockMessages.Length)];
                var daysAgo = random.Next(0, 7);
                if (daysAgo == 0)
                {
                    patientInfo.MostRecentMessageTime = "Today";
                }
                else if (daysAgo == 1)
                {
                    patientInfo.MostRecentMessageTime = "Yesterday";
                }
                else
                {
                    patientInfo.MostRecentMessageTime = $"{daysAgo} days ago";
                }
            }

            patients.Add(patientInfo);
        }
    }

    /// <summary>
    /// Selects a patient and loads mock messages for display.
    /// Author: 2402513
    /// </summary>
    /// <param name="patientId">The ID of the patient to select.</param>
    private void SelectPatient(Guid patientId)
    {
        selectedPatientId = patientId;
        selectedPatient = patients?.FirstOrDefault(p => p.PatientId == patientId)?.Patient;

        // Generate mock conversation for selected patient
        if (selectedPatient != null)
        {
            mockMessages = new List<MockMessage>
            {
                new MockMessage
                {
                    Text = "Hello, I wanted to check in on your recent pressure readings.",
                    Timestamp = "2 days ago",
                    IsFromPatient = false
                },
                new MockMessage
                {
                    Text = "Thank you for checking in. I've been feeling better lately.",
                    Timestamp = "1 day ago",
                    IsFromPatient = true
                },
                new MockMessage
                {
                    Text = "That's great to hear. Keep monitoring your readings and let me know if you notice any changes.",
                    Timestamp = "1 day ago",
                    IsFromPatient = false
                },
                new MockMessage
                {
                    Text = "Will do, thank you!",
                    Timestamp = "Today",
                    IsFromPatient = true
                }
            };
        }
        else
        {
            mockMessages = new List<MockMessage>();
        }
    }

    /// <summary>
    /// Handles sending a message (UI mockup only).
    /// Author: 2402513
    /// </summary>
    /// <remarks>
    /// In a real implementation, this would save the message to a database.
    /// For now, it just adds the message to the mock list for display.
    /// </remarks>
    private void SendMessage()
    {
        if (string.IsNullOrWhiteSpace(newMessage) || selectedPatient == null)
            return;

        // Add message to mock list (UI only, not persisted)
        mockMessages.Add(new MockMessage
        {
            Text = newMessage,
            Timestamp = "Just now",
            IsFromPatient = false
        });

        newMessage = "";
    }
}

