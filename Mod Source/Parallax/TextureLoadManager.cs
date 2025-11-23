using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Networking;

namespace Parallax;


public struct TextureLoadOptions()
{
    /// <summary>
    /// The asset bundle to load textures from. If <c>null</c> then textures
    /// will be loaded directly from the paths on the file system.
    /// </summary>
    public string AssetBundle;

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
}

[KSPAddon(KSPAddon.Startup.Instantly, once: true)]
public class TextureLoadManager : MonoBehaviour
{
    public static TextureLoadManager Instance { get; private set; }
        
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
    public static TextureHandle LoadTexture(string path, TextureLoadOptions options) =>
        Instance.LoadTextureImpl(path, options);

    /// <summary>
    /// Start loading a texture in the background.
    /// </summary>
    /// <param name="path">The path of the texture relative to GameData</param>
    /// <returns></returns>
    public static TextureHandle LoadTexture(string path) =>
        LoadTexture(path, new() { unreadable = true });

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
    public readonly struct TextureHandle : IDisposable
    {
        readonly CacheEntry entry;

        public string Path => entry?.key.path;
        public string AssetBundle => entry?.key.assetBundle;

        /// <summary>
        /// Get the texture handle. If the texture is not loaded yet then
        /// this will block until loading completes.
        /// </summary>
        public Texture2D Texture
        {
            get
            {
                if (entry is null)
                    return null;

                if (!entry.IsComplete)
                    entry.Complete();

                return entry.Texture;
            }
        }

        public bool IsComplete => entry?.IsComplete ?? true;

        /// <summary>
        /// Get a <see cref="CustomYieldInstruction"/> that can be used to
        /// wait until the texture has been loaded.
        /// </summary>
        /// <returns></returns>
        public TextureHandleYieldInstruction Wait() => new(this);

        /// <summary>
        /// Acquire a new handle for the same texture, increasing the
        /// reference count.
        /// </summary>
        /// <returns></returns>
        public TextureHandle Acquire()
        {
            entry?.Acquire();
            return this;
        }

        /// <summary>
        /// Take ownership of the <see cref="Texture2D"/> from the shared cache.
        /// </summary>
        /// <returns></returns>
        public Texture2D Leak()
        {
            if (entry is null)
                return null;

            entry.refcount += 1;
            // Remove the entry from the cache so that it will get naturally
            // freed by unity when all existing references are gone.
            Instance?.TextureCache.Remove(entry.key);

            return Texture;
        }

        public void Dispose()
        {
            entry?.Release();
        }

        private TextureHandle(CacheEntry entry) => this.entry = entry;

        /// <summary>
        /// For internal use only. Don't use this outside of TextureLoadManager.
        /// </summary>
        /// <param name="entry"></param>
        /// <returns></returns>
        internal static TextureHandle Acquire(object entry)
        {
            var centry = (CacheEntry) entry;
            centry.Acquire();
            return new TextureHandle(centry);
        }
    }

    public class TextureHandleYieldInstruction : CustomYieldInstruction
    {
        readonly TextureHandle handle;

        public override bool keepWaiting => handle.IsComplete;

        public TextureHandleYieldInstruction(TextureHandle handle)
            => this.handle = handle;
    }

    #region Texture Cache
    internal struct CacheKey
    {
        public string assetBundle;
        public string path;

        public override int GetHashCode()
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
        public Texture2D texture;
        public Exception exception;
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

        public Texture2D Texture
        {
            get
            {
                if (!(exception is null))
                    throw exception;

                return texture;
            }
        }

        public bool IsComplete => Status != LoadStatus.Pending;

        public void SetTexture(Texture2D texture) => this.texture = texture;

        public void SetException(Exception exception) => this.exception = exception;

        public void Complete()
        {
            if (coroutine is null)
                return;

            while (!IsComplete)
            {
                if (completeHandler is null)
                    break;

                completeHandler.Complete();
                completeHandler = null;

                coroutine.MoveNext();
            }
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

            if (texture is null)
                return;

            TextureLoadManager.Instance?.TextureCache.Remove(key);
            Destroy(texture);
        }
    }

    readonly Dictionary<CacheKey, CacheEntry> TextureCache = [];

    #endregion
    
    #region Implementation
    TextureHandle LoadTextureImpl(string path, TextureLoadOptions options)
    {
        var key = new CacheKey
        {
            assetBundle = options.AssetBundle,
            path = path
        };
        if (TextureCache.TryGetValue(key, out var entry))
            return TextureHandle.Acquire(entry);

        entry = new CacheEntry
        {
            key = key,
            refcount = 0,
        };

        TextureCache.Add(key, entry);
        var coro = CatchExceptions(DoLoadTexture(entry, path, options), entry);
        entry.coroutine = coro;

        StartCoroutine(coro);
        return TextureHandle.Acquire(entry);
    }

    IEnumerator DoLoadTexture(CacheEntry entry, string path, TextureLoadOptions options)
    {
        using var coroguard = new EntryClearGuard(entry);
        // Ensure that there's always one reference count when running. That
        // way if the user has already disposed of the handle then the texture
        // will be destroyed on completion.
        using var texguard = TextureHandle.Acquire(entry);

        // We try to load from the asset bundle first.
        if (options.AssetBundle is not null)
        {
            var handle = LoadAssetBundleAsync(options.AssetBundle);
            entry.completeHandler = handle;
            yield return handle;

            var bundle = handle.Bundle;
            var assetreq = bundle.LoadAssetAsync<Texture2D>(path);
            entry.completeHandler = new AssetBundleRequestCompleteHandler(assetreq);
            yield return assetreq;

            if (assetreq.asset is Texture2D texture)
            {
                entry.SetTexture(texture);
                yield break;
            }
        }

        // If not present then try to load it as a loose file lying around.
        path = GetAbsolutePath(path);

        // DDS textures cannot be handled with UnityWebRequest and need special handling.
        if (path.EndsWith(".dds"))
        {
            // We don't support loading DDS textures asynchronously yet.
            entry.SetTexture(TextureLoader.LoadDDSTexture(path, options.linear, options.unreadable));
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

            entry.SetTexture(DownloadHandlerTexture.GetContent(uwr));
        }
    }
    #endregion

    #region Asset Bundles
    class AssetBundleHandle : ISetException, ICompleteHandler
    {
        public AssetBundleCreateRequest request;
        AssetBundle bundle;
        Exception exception;
        public int refcount;

        public AssetBundle Bundle
        {
            get
            {
                if (!(exception is null))
                    throw exception;
                return bundle;
            }
        }
        public bool IsComplete => !(exception is null) || !(bundle is null);

        public AssetBundleHandle(AssetBundleCreateRequest request)
        {
            this.request = request;
        }

        public AssetBundleHandle(AssetBundle bundle)
        {
            this.bundle = bundle;
        }

        public void SetBundle(AssetBundle bundle) => this.bundle = bundle;

        public void SetException(Exception exception) => this.exception = exception;

        public void Complete()
        {
            if (request.assetBundle == null)
                SetException(new Exception("Failed to load asset bundle"));
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

        var request = AssetBundle.LoadFromFileAsync(GetAbsolutePath(path));
        handle = new AssetBundleHandle(request);

        ActiveBundles.Add(path, handle);
        StartCoroutine(CatchExceptions(DoLoadAssetBundle(handle), handle));
        StartCoroutine(DelayedAssetBundleCleanup(handle, path));
        return handle;
    }

    IEnumerator DoLoadAssetBundle(AssetBundleHandle handle)
    {
        using (var guard = handle.Guard())
        {
            var request = handle.request;
            yield return request;
            handle.Complete();
        }
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
        yield return handle.Guard();

        // Wait 5 frames before unloading the asset bundle so we can coalesce
        // multiple loads from the same bundle together.
        int count = 0;
        while (count < 5)
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

    class JobCompleteHandler : ICompleteHandler
    {
        JobHandle job;

        public JobCompleteHandler(JobHandle job) => this.job = job;

        public void Complete() => job.Complete();
    }

    class WebRequestCompleteHandler : ICompleteHandler
    {
        UnityWebRequest request;

        public WebRequestCompleteHandler(UnityWebRequest request) => this.request = request;

        public void Complete()
        {
            // Does this work?
            while (!request.isDone) { }
        }
    }

    class AssetBundleRequestCompleteHandler : ICompleteHandler
    {
        AssetBundleRequest request;

        public AssetBundleRequestCompleteHandler(AssetBundleRequest request) => this.request = request;

        public void Complete() => _ = request.asset;
    }

    #endregion

    interface ISetException
    {
        void SetException(Exception exception);
    }

    static IEnumerator CatchExceptions(IEnumerator enumerator, ISetException handler)
    {
        using (var dispose = enumerator as IDisposable)
        {
            while (true)
            {
                try
                {
                    if (!enumerator.MoveNext())
                        break;
                }
                catch (Exception e)
                {
                    handler.SetException(e);
                    break;
                }

                yield return enumerator.Current;
            }
        }
    }

    static string GetAbsolutePath(string path)
    {
        return Path.Combine(KSPUtil.ApplicationRootPath, "GameData", path);
    }

    class TextureDisposeGuard : IDisposable
    {
        public Texture2D texture;

        public TextureDisposeGuard(Texture2D texture)
        {
            this.texture = texture;
        }

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