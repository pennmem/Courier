using System;
using UnityEngine;

/// <summary>
/// Helper for reading query-string parameters from the WebGL launch URL
/// (e.g. PROLIFIC_PID, SESSION, videoBaseUrl). Returns null when the build is
/// not running in a browser or the key is absent.
/// </summary>
public static class UrlParams
{
    public static string Get(string key)
    {
        string absoluteUrl = Application.absoluteURL;
        if (string.IsNullOrEmpty(absoluteUrl))
            return null;

        int queryStart = absoluteUrl.IndexOf('?');
        if (queryStart < 0 || queryStart >= absoluteUrl.Length - 1)
            return null;

        int queryEnd = absoluteUrl.IndexOf('#', queryStart + 1);
        string query = queryEnd >= 0
            ? absoluteUrl.Substring(queryStart + 1, queryEnd - queryStart - 1)
            : absoluteUrl.Substring(queryStart + 1);

        string[] pairs = query.Split('&');
        foreach (string pair in pairs)
        {
            string[] parts = pair.Split(new char[] { '=' }, 2);
            if (parts.Length == 0 || parts[0] != key)
                continue;

            string value = parts.Length > 1 ? parts[1] : "";
            return Uri.UnescapeDataString(value.Replace("+", " "));
        }

        return null;
    }
}
