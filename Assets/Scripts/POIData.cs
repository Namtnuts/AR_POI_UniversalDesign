using System;
using System.Collections.Generic;

[Serializable]
public class LocationData
{
    public double lat;
    public double lng;
}

[Serializable]
public class GeometryData
{
    public LocationData location;
}

[Serializable]
public class PlaceItem
{
    public string name;
    public string place_id;
    public double rating;
    public GeometryData geometry;
    public List<string> types;
}

[Serializable]
public class GooglePlacesResponse
{
    public List<PlaceItem> results;
    public string status;
}