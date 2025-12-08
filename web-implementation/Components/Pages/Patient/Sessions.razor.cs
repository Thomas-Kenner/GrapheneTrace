namespace GrapheneTrace.Web.Components.Pages.Patient;

/// <summary>
/// Sessions Page
/// Author: SID:2402513
///
/// Purpose:
/// View past monitoring sessions with detailed pressure maps,
/// comments, and alert history.
/// </summary>
public partial class Sessions
{
    private int selectedSession = 1;

    private void SelectSession(int sessionNumber)
    {
        selectedSession = sessionNumber;
    }
}
