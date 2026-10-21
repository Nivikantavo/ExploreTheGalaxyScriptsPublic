using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public static class PlatformCurrencyIconCache
{
    private static readonly Dictionary<string, Sprite> Loaded = new(StringComparer.Ordinal);
    private static readonly HashSet<string> Failed = new(StringComparer.Ordinal);

    public static bool TryGet(string url, out Sprite sprite)
    {
        if (string.IsNullOrEmpty(url))
        {
            sprite = null;
            return false;
        }

        return Loaded.TryGetValue(url, out sprite) && sprite != null;
    }

    public static IEnumerator Load(string url, Action<Sprite> onCompleted)
    {
        if (string.IsNullOrEmpty(url))
        {
            onCompleted?.Invoke(null);
            yield break;
        }

        if (Loaded.TryGetValue(url, out Sprite cached) && cached != null)
        {
            onCompleted?.Invoke(cached);
            yield break;
        }

        if (Failed.Contains(url))
        {
            onCompleted?.Invoke(null);
            yield break;
        }

        using UnityWebRequest request = UnityWebRequestTexture.GetTexture(url);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Failed.Add(url);
            Debug.LogWarning($"[IAP] Failed to load currency icon: {url} ({request.error})");
            onCompleted?.Invoke(null);
            yield break;
        }

        Texture2D texture = DownloadHandlerTexture.GetContent(request);
        if (texture == null)
        {
            Failed.Add(url);
            onCompleted?.Invoke(null);
            yield break;
        }

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            100f);

        Loaded[url] = sprite;
        onCompleted?.Invoke(sprite);
    }
}
