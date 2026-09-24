using System.Collections.Generic;

public interface IPOIHistoryView
{
    void RefreshHistory(List<HistoryEntry> entries, float maxDistanceLimit);
    void ClearView();
}

[System.Serializable]
public class HistoryEntry
{
    public string title;
    public string category;
    public float rating;
    public UnityEngine.Vector3 passWorldPosition; // Pozycja w świecie w momencie minięcia
}