using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class GPSCoord
{
    public double latitude;
    public double longitude;

    public GPSCoord(double lat, double lon)
    {
        latitude = lat;
        longitude = lon;
    }
}

[Serializable]
public class POIRecord
{
    public string placeId;
    public string placeName;
    public string category;
    public float rating;
    public double latitude;
    public double longitude;

    // Obliczone metryczne przesunięcie względem punktu startowego (Origin GPS)
    public float localX;
    public float localZ;
}

[Serializable]
public class POIRouteContainer
{
    public string routeName;
    public GPSCoord originGPS;
    public List<POIRecord> pois = new List<POIRecord>();

    /// <summary>
    /// Konwersja współrzędnych geograficznych (WGS84) na metryczne współrzędne lokalne Unity (X, Z).
    /// </summary>
    public static Vector2 GPSToMeters(double lat, double lon, double originLat, double originLon)
    {
        const double EarthRadius = 6371000.0; // Promień Ziemi w metrach
        double dLat = (lat - originLat) * Mathf.Deg2Rad;
        double dLon = (lon - originLon) * Mathf.Deg2Rad;

        float z = (float)(dLat * EarthRadius);
        float x = (float)(dLon * EarthRadius * Math.Cos(originLat * Mathf.Deg2Rad));

        return new Vector2(x, z);
    }
}