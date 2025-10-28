namespace IPTVGuideDog.Web.Application;

public sealed class ChannelWorkspaceState
{
    private readonly HashSet<string> _selectedGroups = new(StringComparer.OrdinalIgnoreCase);

    public event Action? Changed;

    public IReadOnlyCollection<string> SelectedGroups => _selectedGroups.ToArray();

    public bool IsSelected(string groupName) => _selectedGroups.Contains(groupName);

    public void ToggleGroup(string groupName)
    {
        if (_selectedGroups.Remove(groupName))
        {
            Changed?.Invoke();
            return;
        }

        _selectedGroups.Add(groupName);
        Changed?.Invoke();
    }

    public void SetSelectedGroups(IEnumerable<string> groupNames)
    {
        _selectedGroups.Clear();
        foreach (var group in groupNames)
        {
            _selectedGroups.Add(group);
        }

        Changed?.Invoke();
    }
}
