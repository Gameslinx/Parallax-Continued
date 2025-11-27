using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.ExceptionServices;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Profiling;

namespace Parallax;

/// <summary>
/// Hints about how the application intends to load textures.
/// </summary>
public enum TextureLoadHint
{
    /// <summary>
    /// Load everything async. Both the asset bundle and textures will be loaded
    /// using their async variants.
    /// </summary>
    ///
    /// <remarks>
    /// This tends to result in unity spacing the texture loads out so that only
    /// one completes every frame. Unless you are loading LZMA-compressed asset
    /// bundles it is recommended to use <see cref="BatchedSync"/> instead,
    /// since loading the asset bundle is very quick and it will result in much
    /// lower overall latency.
    /// </remarks>
    Asynchronous,

    /// <summary>
    /// Load the asset bundle synchronously but load the textures within
    /// asynchronously.
    /// </summary>
    ///
    /// <remarks>
    /// This is the default because it starts all the texture loads immediately,
    /// which results in much lower overall latency when running during scene
    /// switch. Otherwise you will need to wait until <c>Update</c> is called
    /// before any texture loads will even start.
    /// </remarks>
    BatchedSync,

    /// <summary>
    /// Do everything synchronously. Only use this if you are loading a single
    /// texture and intend to immediately block on it.
    /// </summary>
    Synchronous,
}

public struct TextureLoadOptions()
{
    /// <summary>
    /// The asset bundle to load textures from. If <c>null</c> then textures
    /// will be loaded directly from the paths on the file system.
    /// </summary>
    public string assetBundle;

    /// <summary>
    /// Whether this texture should be loaded as if it was a linear format
    /// or whether it has gamma correction.
    ///
    /// This only has an effect when loading a dds texture. Textures in
    /// asset bundles will use the linear setting within the asset bundle
    /// and it is not possible to configure this for png textures.
    /// </summary>
    public bool linear;

    /// <summary>
    /// Whether to mark this texture as non-readable once it is uploaded.
    /// This has no effect when loading from asset bundles, as they use
    /// the configured setting within the asset bundle.
    /// </summary>
    public bool unreadable = true;

    /// <summary>
    /// Hints about how this texture is going to be loaded. This allows the
    /// texture loader to optimize what operations it performs to give the
    /// minimum latency.
    /// </summary>
    public TextureLoadHint hint = TextureLoadHint.BatchedSync;
}

[KSPAddon(KSPAddon.Startup.Instantly, once: true)]
public class TextureLoadManager : MonoBehaviour
{
    public static TextureLoadManager Instance { get; private set; }

    /// <summary>
    /// How many frames to wait before unloading an asset bundle.
    /// </summary>
    /// 
    /// <remarks>
    /// Unloading an asset bundle while asset loads are happening in the
    /// background will block until any queued background reads have completed.
    /// This value should be set high enough that it doesn't overlap with async
    /// loads but no higher than that.
    /// 
    /// Note that keeping an asset bundle loaded prevents any of its assets
    /// from being unloaded, so you cannot set this arbitrarily high.
    /// </remarks>
    public static int AssetBundleUnloadDelay = 30;
        
    void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(this);
    }

    /// <summary>
    /// Start loading a texture in the background.
    /// </summary>
    /// <param name="path">The path of the texture relative to GameData</param>
    /// <param name="options">Options controlling how the texture is loaded.</param>
    /// <returns>A <see cref="LoadRequest"/> which can be used to get the resulting texture.</returns>
    public static TextureHandle<Texture2D> LoadTexture(string path, TextureLoadOptions options) =>
        Instance.LoadTextureImpl<Texture2D>(path, options);

    /// <summary>
    /// Start loading a texture in the background.
    /// </summary>
    /// <param name="path">The path of the texture relative to GameData</param>
    /// <returns></returns>
    public static TextureHandle<Texture2D> LoadTexture(string path) =>
        LoadTexture(path, new() { unreadable = true });

    /// <summary>
    /// Load a cubemap texture in the background.
    /// </summary>
    /// <param name="path">The path of the texture relative to GameData</param>
    /// <param name="options">Options controlling how the texture is loaded.</param>
    /// <returns>A <see cref="LoadRequest"/> which can be used to get the resulting texture.</returns>
    /// <remarks>
    /// If the cubemap is in an asset bundle then the texture loader will attempt
    /// to load it directly as a cubemap, otherwise it will fall back to calling
    /// <see cref="TextureLoader.CubemapFromTexture2D(Texture2D)"/>.
    /// </remarks>
    public static TextureHandle<Cubemap> LoadCubemap(string path, TextureLoadOptions options) =>
        Instance.LoadTextureImpl<Cubemap>(path, options);

    /// <summary>
    /// Load a cubemap texture in the background.
    /// </summary>
    /// <param name="path">The path of the texture relative to GameData</param>
    /// <param name="options">Options controlling how the texture is loaded.</param>
    /// <returns>A <see cref="LoadRequest"/> which can be used to get the resulting texture.</returns>
    /// <remarks>
    /// If the cubemap is in an asset bundle then the texture loader will attempt
    /// to load it directly as a cubemap, otherwise it will fall back to calling
    /// <see cref="TextureLoader.CubemapFromTexture2D(Texture2D)"/>.
    /// </remarks>
    public static TextureHandle<Cubemap> LoadCubemap(string path) =>
        LoadCubemap(path, new() { unreadable = true });

    /// <summary>
    /// Remove all textures from the cache. This doesn't destroy them immediately,
    /// but any ones that are actually unused will be destroyed on the next scene
    /// switch.
    /// </summary>
    public static void ResetCache()
    {
        Instance?.TextureCache?.Clear();
    }

    /// <summary>
    /// A reference-counted handle to a texture.
    /// </summary>
    /// 
    /// <remarks>
    /// You can increment the refcount (and get a new handle) by calling
    /// <see cref="Acquire"/>. Calling <see cref="Dispose"/> will decrement
    /// the refcount and destroy it once it hits zero.
    /// </remarks>
    public readonly struct TextureHandle<T> : IDisposable
        where T : Texture
    {
        readonly CacheEntry entry;

        public string Path => entry?.key.path;

        /// <summary>
        /// Get the texture handle. If the texture is not loaded yet then
        /// this will block until loading completes.
        /// </summary>
        public T GetTexture()
        {
            if (entry is null)
                return null;

            if (!entry.IsComplete)
                entry.Complete();

            return (T)entry.Texture;
        }

        public bool IsComplete => entry?.IsComplete ?? true;

        public int ReferenceCount => entry?.refcount ?? 0;

        /// <summary>
        /// Get a <see cref="CustomYieldInstruction"/> that can be used to
        /// wait until the texture has been loaded.
        /// </summary>
        /// <returns></returns>
        public TextureHandleYieldInstruction Wait() => new(entry);

        /// <summary>
        /// Acquire a new handle for the same texture, increasing the
        /// reference count.
        /// </summary>
        /// <returns></returns>
        public TextureHandle<T> Acquire()
        {
            entry?.Acquire();
            return this;
        }

        /// <summary>
        /// Take ownership of the <see cref="Texture2D"/> from the shared cache.
        /// </summary>
        /// <returns></returns>
        public T Leak()
        {
            if (entry is null)
                return null;

            entry.refcount += 1;
            // Remove the entry from the cache so that it will get naturally
            // freed by unity when all existing references are gone.
            Instance?.TextureCache.Remove(entry.key);

            return GetTexture();
        }

        public void Dispose()
        {
            entry?.Release();
        }

        // Allow converting to a generic texture handle where necessary.
        public static implicit operator TextureHandle<Texture>(TextureHandle<T> handle) =>  
            new(handle.entry);

        private TextureHandle(CacheEntry entry) => this.entry = entry;

        /// <summary>
        /// For internal use only. Don't use this outside of TextureLoadManager.
        /// </summary>
        /// <param name="entry"></param>
        /// <returns></returns>
        internal static TextureHandle<T> Acquire(object entry)
        {
            var centry = (CacheEntry) entry;
            centry.Acquire();
            return new TextureHandle<T>(centry);
        }
    }

    public class TextureHandleYieldInstruction : CustomYieldInstruction
    {
        readonly CacheEntry entry;

        public override bool keepWaiting => !entry.IsComplete;

        // For use internal to TextureLoadManager only.
        internal TextureHandleYieldInstruction(object entry)
            => this.entry = (CacheEntry)entry;
    }

    #region Texture Cache
    internal struct CacheKey
    {
        public string assetBundle;
        public string path;

        public readonly override int GetHashCode()
        {
            return (assetBundle?.GetHashCode() ?? 0) ^ path.GetHashCode();
        }
    }

    class CacheEntry : ISetException
    {
        public enum LoadStatus
        {
            Pending,
            Success,
            Error,
            Empty
        }

        public CacheKey key;
        public Texture texture;
        public ExceptionDispatchInfo exception;
        public int refcount;

        public ICompleteHandler completeHandler;
        public IEnumerator coroutine;

        public LoadStatus Status
        {
            get
            {
                if (exception is not null)
                    return LoadStatus.Error;
                if (texture is not null)
                    return LoadStatus.Success;
                if (coroutine is null)
                    return LoadStatus.Empty;
                return LoadStatus.Pending;
            }
        }

        public Texture Texture
        {
            get
            {
                exception?.Throw();

                return texture;
            }
        }

        public bool IsComplete => Status != LoadStatus.Pending;

        /// <summary>
        /// Set the texture for this entry. If needed, the texture will be
        /// converted to the desired format.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="texture"></param>
        public void SetTexture<T>(Texture texture)
            where T : Texture
        {
            if (typeof(T) == typeof(Cubemap) && texture is Texture2D tex2d)
                this.texture = TextureLoader.CubemapFromTexture2D(tex2d);
            else
                this.texture = texture;
        }

        public void SetException(ExceptionDispatchInfo exception) => this.exception = exception;
        public void SetException(Exception exception) =>
            SetException(ExceptionDispatchInfo.Capture(exception));

        public void Complete()
        {
            if (coroutine is null)
                return;

            Profiler.BeginSample($"TextureLoadManager.Complete: {key.path}");

            while (!IsComplete)
            {
                completeHandler?.Complete();
                completeHandler = null;

                coroutine?.MoveNext();
            }

            Profiler.EndSample();
        }
    
        public void Acquire()
        {
            refcount += 1;
        }

        public void Release()
        {
            refcount -= 1;
            if (refcount != 0)
                return;

            if (Instance is not null
                && Instance.TextureCache.TryGetValue(key, out var entry)
                && ReferenceEquals(entry, this))
            {
                Instance.TextureCache.Remove(key);
            }

            if (texture is null)
                return;

            Destroy(texture);
        }
    }

    readonly Dictionary<CacheKey, CacheEntry> TextureCache = [];

    #endregion
    
    #region Implementation
    TextureHandle<T> LoadTextureImpl<T>(string path, TextureLoadOptions options)
        where T : Texture
    {
        var key = new CacheKey
        {
            assetBundle = options.assetBundle,
            path = path
        };
        if (TextureCache.TryGetValue(key, out var entry))
            return TextureHandle<T>.Acquire(entry);

        if (typeof(T) == typeof(Cubemap))
            ParallaxDebug.Log($"Loading Parallax Cubemap: {path}");
        else
            ParallaxDebug.Log($"Loading Parallax Texture: {path}");

        entry = new CacheEntry
        {
            key = key,
            refcount = 0,
        };

        TextureCache.Add(key, entry);
        var coro = LoadTextureWrapper(
            CatchExceptions(DoLoadTexture<T>(entry, path, options), entry),
            path
        );
        entry.coroutine = coro;

        // Needs to happen before we start the coroutine
        var handle = TextureHandle<T>.Acquire(entry);

        StartCoroutine(coro);
        return handle;
    }

    IEnumerator LoadTextureWrapper(IEnumerator enumerator, string path)
    {
        var marker = new ProfilerMarker($"LoadTexture: {path}");

        while(true)
        {
            using (var scope = marker.Auto())
            {
                if (!enumerator.MoveNext())
                    break;
            }

            yield return enumerator.Current;
        }
    }

    IEnumerator DoLoadTexture<T>(CacheEntry entry, string path, TextureLoadOptions options)
        where T : Texture
    {
        using var coroguard = new EntryClearGuard(entry);
        // Ensure that there's always one reference count when running. That
        // way if the user has already disposed of the handle then the texture
        // will be destroyed on completion.
        using var texguard = TextureHandle<Texture>.Acquire(entry);

        if (typeof(T) == typeof(Cubemap))
            options.unreadable = false;

        // We try to load from the asset bundle first.
        if (options.assetBundle is not null)
        {
            AssetBundleHandle handle;
            // Synchronously load the asset bundle unless the caller explicitly
            // wants it loaded async.
            if (options.hint == TextureLoadHint.Asynchronous)
                handle = LoadAssetBundleAsync(options.assetBundle);
            else
                handle = LoadAssetBundleSync(options.assetBundle);

            using var guard = handle.Guard();
            entry.completeHandler = handle;
            if (!handle.IsComplete)
                yield return handle;

            var bundle = handle.Bundle;

            UnityEngine.Object asset;
            var normalizedPath = NormalizeAssetBundlePath(path);
            if (!bundle.Contains(normalizedPath))
            {
                // Avoid yielding for a frame if the asset bundle doesn't contain
                // the asset we're looking for.
                asset = null;
            }
            else if (options.hint < TextureLoadHint.Synchronous)
            {
                var assetreq = bundle.LoadAssetAsync<Texture>(normalizedPath);
                entry.completeHandler = new AssetBundleRequestCompleteHandler(assetreq);
                if (!assetreq.isDone)
                    yield return assetreq;
                
                asset = assetreq.asset;
            }
            else
            {
                // If there is only one texture being loaded and it is going to
                // immediately be blocked on then we might as well just load
                // synchronously.
                asset = bundle.LoadAsset<Texture>(normalizedPath);
            }

            if (asset is not null)
            {
                // If we directly match the requested texture type then we're good
                // to go.
                if (asset is T tex)
                {
                    entry.SetTexture<T>(tex);
                    yield break;
                }

                if (typeof(T) == typeof(Cubemap) && asset is Texture2D tex2d)
                {
                    entry.SetTexture<T>(tex2d);
                    yield break;
                }

                // Log a warning if we find the asset but it was not in the right format
                ParallaxDebug.LogError($"Texture {path} in asset bundle {options.assetBundle} could not be converted to {typeof(T).Name}");
            }
            else
            {
                // Not a warning because this is not an error.
                // However, it is really useful as a diagnostic if a mod author is
                // expecting the texture to be within the asset bundle.
                ParallaxDebug.Log($"Texture {path} not found in asset bundle {options.assetBundle}. Falling back to on-disk texture.");
            }
        }

        path = GetAbsolutePath(path);

        // DDS textures cannot be handled with UnityWebRequest and need special handling.
        if (path.EndsWith(".dds"))
        {
            Profiler.BeginSample($"LoadDDSTexture ({path})");

            // We don't support loading DDS textures asynchronously yet.
            entry.SetTexture<T>(TextureLoader.LoadDDSTexture(path, options.linear, options.unreadable));

            Profiler.EndSample();
        }
        else
        {
            var url = new Uri(path);
            using var uwr = UnityWebRequestTexture.GetTexture(url, options.unreadable);
            entry.completeHandler = new WebRequestCompleteHandler(uwr);
            yield return uwr.SendWebRequest();

            if (uwr.isNetworkError || uwr.isHttpError)
            {
                entry.SetException(new Exception($"Failed to load texture: {uwr.error}"));
                yield break;
            }

            entry.SetTexture<T>(DownloadHandlerTexture.GetContent(uwr));
        }
    }
    #endregion

    #region Asset Bundles
    class AssetBundleHandle : CustomYieldInstruction, ISetException, ICompleteHandler
    {
        public AssetBundleCreateRequest request;
        AssetBundle bundle;
        ExceptionDispatchInfo exception;
        public int refcount;

        public AssetBundle Bundle
        {
            get
            {
                exception?.Throw();
                return bundle;
            }
        }
        public bool IsComplete => exception is not null || bundle is not null;

        public override bool keepWaiting => !IsComplete;

        public AssetBundleHandle(AssetBundleCreateRequest request)
        {
            this.request = request;
        }

        public AssetBundleHandle(AssetBundle bundle)
        {
            this.bundle = bundle;
        }

        public void SetBundle(AssetBundle bundle) => this.bundle = bundle;

        public void SetException(ExceptionDispatchInfo exception) => this.exception = exception;

        public void Complete()
        {
            if (IsComplete)
                return;

            if (request?.assetBundle == null)
                throw new Exception("Failed to load asset bundle");
            else
                SetBundle(request.assetBundle);
        }

        public AssetBundle GetBundle() => bundle;

        public RefCountGuard Guard() => new RefCountGuard(this);

        public class RefCountGuard : CustomYieldInstruction, IDisposable
        {
            readonly AssetBundleHandle handle;

            public override bool keepWaiting => !handle.IsComplete;

            public RefCountGuard(AssetBundleHandle handle)
            {
                this.handle = handle;
                handle.refcount += 1;
            }

            public void Dispose()
            {
                handle.refcount -= 1;
            }
        }
    }

    readonly Dictionary<string, AssetBundleHandle> ActiveBundles = new Dictionary<string, AssetBundleHandle>();

    AssetBundleHandle LoadAssetBundleAsync(string path)
    {
        if (ActiveBundles.TryGetValue(path, out var handle))
            return handle;

        ParallaxDebug.Log($"Loading Asset Bundle: {path}");

        var request = AssetBundle.LoadFromFileAsync(GetAbsolutePath(path));
        handle = new AssetBundleHandle(request);

        ActiveBundles.Add(path, handle);
        StartCoroutine(LoadAssetBundleWrapper(
            CatchExceptions(DoLoadAssetBundle(handle), handle),
            path
        ));
        StartCoroutine(DelayedAssetBundleCleanup(handle, path));
        return handle;
    }

    AssetBundleHandle LoadAssetBundleSync(string path)
    {
        if (ActiveBundles.TryGetValue(path, out var handle))
            return handle;

        ParallaxDebug.Log($"Loading Asset Bundle: {path}");
        
        var bundle = AssetBundle.LoadFromFile(GetAbsolutePath(path));
        handle = new AssetBundleHandle(bundle);
        if (bundle == null)
            handle.SetException(ExceptionDispatchInfo.Capture(new Exception("Failed to load asset bundle")));

        ActiveBundles.Add(path, handle);
        StartCoroutine(DelayedAssetBundleCleanup(handle, path));
        return handle;
    }

    // The goal of this method is purely to make sure that the correct spans
    // appear in the profiler.
    IEnumerator LoadAssetBundleWrapper(IEnumerator enumerator, string path)
    {
        var marker = new ProfilerMarker($"LoadAssetBundle: {path}");

        while(true)
        {
            using (var scope = marker.Auto())
            {
                if (!enumerator.MoveNext())
                    break;
            }

            yield return enumerator.Current;
        }
    }

    IEnumerator DoLoadAssetBundle(AssetBundleHandle handle)
    {
        using var guard = handle.Guard();
        var request = handle.request;
        yield return request;
        handle.Complete();
    }

    /// <summary>
    /// Asset bundles need to be explicitly unloaded if they are not attached
    /// to a scene. This coroutine waits 5 frames after the bundle is completed
    /// so that other requests in the same frames can reuse it.
    /// </summary>
    /// <param name="handle"></param>
    /// <param name="path"></param>
    /// <returns></returns>
    IEnumerator DelayedAssetBundleCleanup(AssetBundleHandle handle, string path)
    {
        yield return new WaitForEndOfFrame();
        yield return handle;

        // Wait 5 frames before unloading the asset bundle so we can coalesce
        // multiple loads from the same bundle together.
        int count = 0;
        while (count < AssetBundleUnloadDelay)
        {
            if (handle.refcount != 0)
                count = 0;
            else
                count += 1;

            yield return null;
        }

        ActiveBundles.Remove(path);

        var bundle = handle.GetBundle();
        if (bundle is null)
            yield break;

        bundle.Unload(false);
    }
    #endregion

    #region ICompleteHandler
    // All the stuff in this section is to allow us to complete texture loading
    // synchronously, even though we are using coroutines behind the scenes.
    //
    // The various derived classes of ICompleteHandler are for various things
    // that we are waiting on to complete.

    interface ICompleteHandler
    {
        void Complete();
    }

    class WebRequestCompleteHandler(UnityWebRequest request) : ICompleteHandler
    {
        public void Complete()
        {
            while (!request.isDone) { }
        }
    }

    class AssetBundleRequestCompleteHandler(AssetBundleRequest request) : ICompleteHandler
    {
        public void Complete() => _ = request.asset;
    }

    #endregion

    interface ISetException
    {
        void SetException(ExceptionDispatchInfo exception);
    }

    static IEnumerator CatchExceptions(IEnumerator enumerator, ISetException handler)
    {
        using var dispose = enumerator as IDisposable;

        while (true)
        {
            try
            {
                if (!enumerator.MoveNext())
                    break;
            }
            catch (Exception e)
            {
                handler.SetException(ExceptionDispatchInfo.Capture(e));
                break;
            }

            yield return enumerator.Current;

        }
    }

    static string GetAbsolutePath(string path)
    {
        return Path.Combine(KSPUtil.ApplicationRootPath, "GameData", path);
    }

    // Normalize the asset bundle path so config writers don't need to worry
    // about the specific path separator they use.
    static string NormalizeAssetBundlePath(string path)
    {
        // Normalize all \ separators to /, then convert the name to lowercase.
        // This matches what is exported by the asset bundle script:
        // - The script normalizes all path separators to be /
        // - Unity converts all asset bundle names to lowercase.

        return path
            .Replace('\\', '/')
            .ToLowerInvariant();
    }

    class TextureDisposeGuard(Texture2D texture) : IDisposable
    {
        public Texture2D texture = texture;

        public void Clear() => texture = null;

        public void Dispose()
        {
            if (texture is not null)
                Destroy(texture);
        }
    }

    class EntryClearGuard(CacheEntry entry) : IDisposable
    {
        public void Dispose()
        {
            entry.coroutine = null;
            entry.completeHandler = null;
        }
    }
}