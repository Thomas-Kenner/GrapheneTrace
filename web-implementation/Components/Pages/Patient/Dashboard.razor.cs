using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace GrapheneTrace.Web.Components.Pages.Patient;

/// <summary>
/// Patient Dashboard Page
/// Author: SID:2402513
///
/// Purpose:
/// Main patient dashboard providing real-time pressure monitoring, alerts,
/// device status, and weekly insights.
/// </summary>
public partial class Dashboard
{
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    private string userName = "";
    private string randomQuote = "";

    private readonly string[] inspirationalQuotes = new[]
    {
        "Every step towards better health is a victory worth celebrating.",
        "Your commitment to monitoring your health today shapes a healthier tomorrow.",
        "Small, consistent actions lead to remarkable transformations.",
        "Wellness is a journey, not a destination. You're on the right path.",
        "Taking care of yourself is the most important investment you can make.",
        "Progress, not perfection. Every day is a new opportunity.",
        "Your health data tells a story of resilience and dedication.",
        "Prevention is the best medicine, and you're taking charge.",
        "Consistency is the bridge between goals and accomplishment.",
        "Trust the process. Your efforts are making a difference."
    };

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        userName = authState.User.Identity?.Name ?? "Unknown";

        var random = new Random();
        randomQuote = inspirationalQuotes[random.Next(inspirationalQuotes.Length)];
    }
}
