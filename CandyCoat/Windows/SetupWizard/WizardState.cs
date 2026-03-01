using CandyCoat.Data;

namespace CandyCoat.Windows.SetupWizard;

internal sealed class WizardState
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName  { get; set; } = string.Empty;
    public string HomeWorld { get; set; } = string.Empty;

    public string ProfileId    { get; set; } = string.Empty;
    public bool   IdGenerated  { get; set; } = false;

    public string    UserMode             { get; set; } = string.Empty;
    public StaffRole SelectedPrimaryRole  { get; set; } = StaffRole.None;
    public StaffRole SelectedSecondaryRoles { get; set; } = StaffRole.None;
    public bool      MultiRoleToggle      { get; set; } = false;
    public bool      RolePasswordUnlocked { get; set; } = false;
    public string    RolePasswordBuffer   { get; set; } = string.Empty;
}
