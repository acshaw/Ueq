using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// The single seam through which all DB-referenced Unity assets are loaded (2.1, D5).
///
/// Content rows store a <b>string address</b> (e.g. <c>"sword_01"</c>) — never a prefab/sprite,
/// which can't live in a database. This resolver turns that address back into the real asset.
/// Callers go through here and never touch Addressables directly, so the loader is swappable in
/// one place (Addressables today; if the strategy ever changes, only this file does).
///
/// <b>Async-first:</b> unlike <c>Resources.Load</c>'s synchronous return, Addressables hands back
/// an awaitable handle. Prefer <see cref="LoadAsync{T}"/>. <see cref="LoadBlocking{T}"/> exists for
/// the server-startup path where content must be present before the world goes live; it forces
/// completion and should not be used on the gameplay tick.
///
/// The resolver owns handle lifetime: it caches one handle per address so repeated loads share,
/// and <see cref="Release"/> / <see cref="ReleaseAll"/> free them (e.g. on zone unload).
/// </summary>
public static class AssetResolver
{
    static readonly Dictionary<string, AsyncOperationHandle> _handles = new();

    /// <summary>Resolve an asset by its Addressables address. Returns null if the address is unknown.</summary>
    public static async Task<T> LoadAsync<T>(string address) where T : Object
    {
        if (string.IsNullOrEmpty(address))
            return null;

        var handle = GetOrStartLoad<T>(address);
        await handle.Task;

        if (handle.Status != AsyncOperationStatus.Succeeded)
        {
            Debug.LogWarning($"[Assets] Failed to resolve address '{address}'.");
            return null;
        }
        return handle.Result as T;
    }

    /// <summary>
    /// Synchronous resolve for the startup path only (forces the async load to complete).
    /// Avoid on the gameplay tick — it can stall the frame.
    /// </summary>
    public static T LoadBlocking<T>(string address) where T : Object
    {
        if (string.IsNullOrEmpty(address))
            return null;

        var handle = GetOrStartLoad<T>(address);
        var result = handle.WaitForCompletion();
        if (handle.Status != AsyncOperationStatus.Succeeded)
        {
            Debug.LogWarning($"[Assets] Failed to resolve address '{address}'.");
            return null;
        }
        return result;
    }

    static AsyncOperationHandle<T> GetOrStartLoad<T>(string address) where T : Object
    {
        if (_handles.TryGetValue(address, out var existing))
            return existing.Convert<T>();

        var handle = Addressables.LoadAssetAsync<T>(address);
        _handles[address] = handle;
        return handle;
    }

    /// <summary>Release a single cached asset by address.</summary>
    public static void Release(string address)
    {
        if (_handles.TryGetValue(address, out var handle))
        {
            Addressables.Release(handle);
            _handles.Remove(address);
        }
    }

    /// <summary>Release every cached asset (e.g. on shutdown or full content unload).</summary>
    public static void ReleaseAll()
    {
        foreach (var handle in _handles.Values)
            Addressables.Release(handle);
        _handles.Clear();
    }
}
