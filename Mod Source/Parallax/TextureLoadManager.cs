using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Networking;

namespace Parallax
{
    [KSPAddon(KSPAddon.Startup.Instantly, once: true)]
    public class TextureLoadManager : MonoBehaviour
    {
        public static TextureLoadManager Instance { get; private set; }
            
        void Awake()
        {
            Instance = this;
            DontDestroyOnLoad(this);
        }

        public struct LoadOptions
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
            public bool unreadable;
        }
        
        /// <summary>
        /// An in-flight background texture load.
        /// </summary>
        public abstract class LoadRequest : CustomYieldInstruction
        {
            public enum LoadStatus
            {
                Pending,
                Success,
                Error,
            }

            /// <summary>
            /// The path passed into this load request.
            /// </summary>
            public string Path;

            /// <summary>
            /// The current status of this load request.
            /// </summary>
            public abstract LoadStatus Status { get; }

            /// <summary>
            /// The loaded texture.
            ///
            /// This will throw an exception on access if the texture failed to
            /// load. You can check whether this will occur by looking at the
            /// <see cref="Status"/> property.
            /// </summary>
            public abstract Texture2D Texture { get; }

            public bool IsComplete => Status != LoadStatus.Pending;

            public sealed override bool keepWaiting => Status == LoadStatus.Pending;

            /// <summary>
            /// Synchronously complete this load request, blocking the main thread
            /// until it is complete.
            /// </summary>
            public abstract void Complete();
        }

        /// <summary>
        /// Start loading a texture in the background.
        /// </summary>
        /// <param name="path">The path of the texture relative to GameData</param>
        /// <param name="options">Options controlling how the texture is loaded.</param>
        /// <returns>A <see cref="LoadRequest"/> which can be used to get the resulting texture.</returns>
        public static LoadRequest LoadTexture(string path, LoadOptions options)
        {
            return Instance.LoadTextureImpl(path, options);
        }

        /// <summary>
        /// Start loading a texture in the background.
        /// </summary>
        /// <param name="path">The path of the texture relative to GameData</param>
        /// <returns></returns>
        public static LoadRequest LoadTexture(string path)
        {
            return LoadTexture(path, new LoadOptions());
        }
        
        #region Implementation
        // Actual implementation details of LoadRequest.
        //
        // This way it all remains hidden from consumers of the class.
        class TextureLoadRequest : LoadRequest, ISetException
        {
            private Texture2D texture;
            private Exception exception;

            public ICompleteHandler completeHandler;
            public IEnumerator coroutine;

            public override LoadStatus Status
            {
                get
                {
                    if (!(exception is null))
                        return LoadStatus.Error;
                    if (!(texture is null))
                        return LoadStatus.Success;
                    return LoadStatus.Pending;
                }
            }

            public override Texture2D Texture
            {
                get
                {
                    if (!(exception is null))
                        throw exception;

                    return texture;
                }
            }

            public TextureLoadRequest(string path)
            {
                Path = path;
            }

            public void SetTexture(Texture2D texture) => this.texture = texture;

            public void SetException(Exception exception) => this.exception = exception;

            public override void Complete()
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
        }


        TextureLoadRequest LoadTextureImpl(string path, LoadOptions options)
        {
            var request = new TextureLoadRequest(path);
            var coro = CatchExceptions(DoLoadTexture(request, path, options), request);
            request.coroutine = coro;

            StartCoroutine(coro);
            return request;
        }

        IEnumerator DoLoadTexture(TextureLoadRequest loadreq, string path, LoadOptions options)
        {
            // We try to load from the asset bundle first.
            if (!(options.AssetBundle is null))
            {
                var handle = LoadAssetBundleAsync(options.AssetBundle);
                loadreq.completeHandler = handle;
                yield return handle;

                var bundle = handle.Bundle;
                var assetreq = bundle.LoadAssetAsync<Texture2D>(path);
                loadreq.completeHandler = new AssetBundleRequestCompleteHandler(assetreq);
                yield return assetreq;

                if (assetreq.asset is Texture2D texture)
                {
                    loadreq.SetTexture(texture);
                    yield break;
                }
            }

            // If not present then try to load it as a loose file lying around.
            path = GetAbsolutePath(path);

            // DDS textures cannot be handled with UnityWebRequest and need special handling.
            if (path.EndsWith(".dds"))
            {
                // We don't support loading DDS textures asynchronously yet.
                loadreq.SetTexture(TextureLoader.LoadDDSTexture(path, options.linear, options.unreadable));
            }
            else
            {
                var url = new Uri(path);
                using (var uwr = UnityWebRequestTexture.GetTexture(url, options.unreadable))
                {
                    loadreq.completeHandler = new WebRequestCompleteHandler(uwr);
                    yield return uwr.SendWebRequest();

                    if (uwr.isNetworkError || uwr.isHttpError)
                    {
                        loadreq.SetException(new Exception($"Failed to load texture: {uwr.error}"));
                        yield break;
                    }

                    loadreq.SetTexture(DownloadHandlerTexture.GetContent(uwr));
                }
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

        public interface ICompleteHandler
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
                if (!(texture is null))
                    Destroy(texture);
            }
        }
    }
}