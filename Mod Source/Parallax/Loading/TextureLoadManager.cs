using System;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using Expansions.Serenity.RobotArmFX;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.IO.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Parallax.Loading;

/// <summary>
/// A manager for handling asynchronous texture uploads.
/// </summary>
///
/// <remarks>
/// Unity makes it remarkably hard to actually load textures asynchronously,
/// unless you put everything into resource bundles. (And even then it is
/// finicky). This class represents the best effort I (@Phantomical) have seen
/// in actually making async texture loading happen in Unity. It might be
/// possible to do better than this by using native plugins
/// (notably CommandBuffer.IssuePluginCustomTextureUpdateV2) but I haven't been
/// able to figure out how to get that to work.
///
/// So how does it work?
///
/// In a "standard" fast DDS texture load there are 4 major steps:
/// * Read the texture off of the disk into a byte array.
/// * Create a new Texture2D with the appropriate size and format.
/// * Load the raw texture data into the texture.
/// * Apply the texture so that it gets updated on the GPU.
///
/// Once you start working with large most textures of these operations are slow.
/// The disk read results in large byte arrays, which cause slow GCs. Basically
/// every Texture2D method (ctor and loading data) seems to result in a large
/// memset or memcpy internally, which causes a bunch of incredibly slow page
/// faults. Apply has some overhead but actually happens to be ok, relatively.
///
/// We replace or improve most of these one-by-one:
///
/// File Reads
/// ==========
/// Unity provides a class called AsyncReadManager which allows you to offload
/// disk reads to a background thread. Instead of calling File.ReadAllBytes we
/// can instead send off a request and get back a JobHandle that notifies us
/// when it is done. An important benefit here is that we can now use
/// NativeArrays instead of managed arrays, so we can avoid the GC entirely.
///
/// Depending on how you sequence things, you could call texture.GetRawTextureData()
/// first and then read the data directly into it, or you could start the read
/// first and then copy the data in later. As it turns out creating the texture
/// and calling GetRawTextureData() are both slow so this implementation chooses
/// to start the disk read first and then do the other main thread work.
///
/// Uninitialized Textures
/// ======================
/// At this (hypothetical) point, if you were to look at a profile for the texture
/// loader you would see that most of the time is spent in two places:
/// - new Texture2D performs a memset to zero out the texture memory
/// - GetRawTextureData copies the entire texture data.
///
/// Since we're going to overwrite the entire texture immediately after, both
/// of these are unnecessary.
///
/// Unity provides no _documented_ way to create an uninitialized texture.
/// However, if you look in the reference source you will find that there are a
/// number of commented flags in the TextureCreationFlags enum. One of them,
/// DontInitializePixels, does exactly what we want.
///
/// CreateUninitializedTexture() takes care of providing the appropriate flags
/// needed to create such a texture.
///
/// Async Copy
/// ==========
/// Since we are now using GetRawTextureData unconditionally, we can move the
/// actual copy of texture data out to a job. This usually doesn't do that much
/// if using a single job, but every bit of main thread time counts once we
/// start trying to batch together multiple texture loads.
///
/// The Rest
/// ========
/// Most of the remaining time ends up being spent in GetRawTextureData. Unity
/// (or at least KSP's version of unity) doesn't really appear to offer any
/// flags to avoid this. It might be possible to use native plugins to avoid
/// this but I haven't been able to figure out a way to do this without causing
/// KSP to segfault.
///
/// That means that this is pretty much the limit for what we can do.
/// 
/// The final procedure implemented here is:
/// * Load the file header in order to get the texture size and format.
/// * Schedule a background job to read the rest of the texture file.
/// * Create an uninitialized texture
/// * Call GetRawTextureData() to get a pointer to the texture data.
/// * Schedule a background job to copy the file data into the texture
///   after the file read is complete.
/// * (async) Wait for the jobs to complete.
/// * Run texture.Apply()
/// 
/// During the (async) step callers are free to start more texture loads. This
/// is actually the ideal way to use this, since the background jobs generally
/// will complete fairly quickly.
/// </remarks>
[KSPAddon(KSPAddon.Startup.Instantly, once: true)]
internal unsafe class TextureLoadManager : MonoBehaviour
{
    public static TextureLoadManager Instance { get; private set; }

    void Awake()
    {
        Instance = this;
        DontDestroyOnLoad(this);
    }

    #region Update
    readonly List<InFlightLoad> inFlight = [];

    void Update()
    {
        int i = 0;
        int j = 0;
        for (; i < inFlight.Count; ++i)
        {
            if (!inFlight[i].MoveNext())
                inFlight[i].Dispose();
            else
                inFlight[j++] = inFlight[i];
        }

        if (i != j)
            inFlight.RemoveRange(j, inFlight.Count - j);
    }
    #endregion

    #region Public API
    public readonly struct LoadHandle
    {
        readonly string path;
        readonly Task<Texture2D> task;
        readonly InFlightLoad inFlight;

        internal LoadHandle(Task<Texture2D> task, InFlightLoad inFlight, string path)
        {
            this.task = task;
            this.path = path;
            this.inFlight = inFlight;
        }

        /// <summary>
        /// Get a load handle that is already complete.
        /// </summary>
        /// <param name="texture"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        internal static LoadHandle Complete(Texture2D texture, string path)
        {
            return new(Task.FromResult(texture), null, path);
        }

        /// <summary>
        /// The texture path for this load handle.
        /// </summary>
        public readonly string Path => path;

        /// <summary>
        /// The loaded texture. Attempting to access this before loading has
        /// completed will throw an exception.
        /// </summary>
        public readonly Texture2D Texture
        {
            get
            {
                if (!IsComplete)
                    throw new InvalidOperationException("LoadHandle is not complete yet");
                return task.Result;
            }
        }

        /// <summary>
        /// Whether this load has completed (either by succeeding or by failing
        /// with an exception).
        /// </summary>
        public readonly bool IsComplete => task.IsCompleted;

        /// <summary>
        /// Block until this texture is loaded and ready (or has failed to load).
        /// </summary>
        public readonly void Complete()
        {
            if (inFlight is null)
                return;

            inFlight.Complete();
        }
    }

    /// <summary>
    /// Load a texture as asynchronously as possible within unity.
    /// </summary>
    /// <param name="path"></param>
    /// <param name="linear"></param>
    /// <param name="unreadable"></param>
    /// <returns></returns>
    /// 
    /// <remarks>
    /// The happy path for this method is an unreadable DDS texture. Anything
    /// other than that will involve some amount of work from the main thread.
    /// </remarks>
    public static LoadHandle LoadTextureAsync(string path, bool linear, bool unreadable = true)
    {
        return Instance.DoLoadTextureAsync(path, linear, unreadable);
    }

    public static Texture2D LoadTexture(string path, bool linear, bool unreadable = true)
    {
        var handle = LoadTextureAsync(path, linear, unreadable);
        handle.Complete();
        return handle.Texture;
    }

    public static LoadHandle CreateCompletedHandle(Texture2D texture, string path)
    {
        return LoadHandle.Complete(texture, path);
    }

    readonly DisposeSlot<InFlightLoad> LoadSlot = new();
    LoadHandle DoLoadTextureAsync(string path, bool linear, bool unreadable = true)
    {
        ParallaxDebug.Log("Loading Parallax Texture: " + path);

        TaskCompletionSource<Texture2D> completion = new();
        Task<Texture2D> task = completion.Task;

        try
        {
            // Ensure that load is properly disposed of if an exception is thrown.
            using var slot = LoadSlot;
            string filePath = ConfigLoader.GameDataPath + path;
            InFlightLoad load;

            if (path.EndsWith(".dds"))
                load = InFlightDDSLoad.Start(filePath, linear, unreadable);
            else
                load = InFlightPNGLoad.Start(filePath, linear, unreadable);
            slot.Value = load;

            load.completion = completion;
            load.path = path;
            if (load.MoveNext())
                inFlight.Add(slot.Take());

            return new LoadHandle(task, load, path);
        }
        catch (Exception e)
        {
            completion.TrySetException(e);
            return new LoadHandle(task, null, path);
        }
    }
    #endregion

    #region In-Flight Loads
    struct TextureLoadData
    {
        public int width, height;
        public TextureFormat format;
        public bool mips;
        public bool linear;
        public bool unreadable;
    }

    internal abstract class InFlightLoad : IDisposable
    {
        public TaskCompletionSource<Texture2D> completion;
        public string path;
        public Texture2D texture;
        public Exception exception;

        public abstract bool IsComplete { get; }
        public bool IsCompleteOrFailed => exception is not null || IsComplete;

        protected abstract bool DoMoveNext();

        protected abstract void DoComplete();

        /// <summary>
        /// Do the next step to load this texture. Returns <c>false</c> if
        /// texture loading is complete.
        /// </summary>
        /// <returns></returns>
        public bool MoveNext()
        {
            try
            {
                if (DoMoveNext())
                    return true;

                completion.SetResult(texture);
            }
            catch (Exception e)
            {
                exception = e;
                completion.TrySetException(e);
            }

            return false;
        }

        /// <summary>
        /// Block until the texture load has completed.
        /// </summary>
        public void Complete()
        {
            try
            {
                DoComplete();
                completion.SetResult(texture);
            }
            catch (Exception e)
            {
                exception = e;
                completion.TrySetException(e);
            }
        }

        public virtual void Dispose()
        {
            completion?.TrySetCanceled();
        }
    }

    class InFlightDDSLoad : InFlightLoad
    {
        enum State
        {
            WaitingOnJob,
            Done,
        }

        const int DDS_HEADER_SIZE = 128;

        static readonly DisposeSlot<InFlightDDSLoad> Slot = new ();

        State state;
        SafeReadHandle handle;
        TextureLoadData textureData;
        JobHandle job;

        public override bool IsComplete => state == State.Done;

        public static InFlightDDSLoad Start(string path, bool linear, bool unreadable = true)
        {
            byte[] header = new byte[DDS_HEADER_SIZE];
            long length;
            using (var file = File.OpenRead(path))
            {
                length = file.Length;
                if (length < DDS_HEADER_SIZE)
                    throw new Exception("File is too small to be a valid DDS texture");
                if (length > int.MaxValue)
                    throw new Exception("DDS texture file is too large to load");

                if (file.Read(header, 0, DDS_HEADER_SIZE) < DDS_HEADER_SIZE)
                    throw new Exception("Unable to read the whole texture header");
            }

            byte ddsSizeCheck = header[4];
            if (ddsSizeCheck != 124)
                throw new Exception("Invalid DDS texture - header size check failed");

            var textureData = GetTextureMetadata(header, linear, unreadable);
            var inFlight = new InFlightDDSLoad()
            {
                path = path,
                texture = CreateUninitializedTexture(in textureData),
                textureData = textureData,
            };
            using var slot = Slot;
            slot.Value = inFlight;
            var fileData = new NativeArray<byte>(
                (int)(length - DDS_HEADER_SIZE),
                // Specifically use TempJob here because Allocator.Temp is a bump
                // allocator and these are large allocations.
                Allocator.TempJob,
                NativeArrayOptions.UninitializedMemory
            );

            ReadCommand command = new()
            {
                Buffer = fileData.GetUnsafePtr(),
                Offset = DDS_HEADER_SIZE,
                Size = length - DDS_HEADER_SIZE
            };
            inFlight.handle = new SafeReadHandle(AsyncReadManager.Read(path, &command, 1));
            inFlight.job = inFlight.handle.JobHandle;

            var copyJob = new CopyJob
            {
                input = fileData,
                output = inFlight.texture.GetRawTextureData<byte>()
            };
            inFlight.job = copyJob.Schedule(inFlight.job);

            inFlight.state = State.WaitingOnJob;
            return slot.Take();
        }

        protected override bool DoMoveNext()
        {
            if (state == State.Done)
                return false;

            if (!job.IsCompleted)
                return true;

            state = State.Done;
            if (handle.Status != ReadStatus.Complete)
                throw new Exception("Failed to read from the texture file");

            texture.Apply(false, textureData.unreadable);
            return false;
        }

        protected override void DoComplete()
        {
            while (DoMoveNext())
                job.Complete();
        }

        public override void Dispose()
        {
            job.Complete();
            handle.Dispose();
        }

        static TextureLoadData GetTextureMetadata(byte[] header, bool linear, bool markUnreadable)
        {
            int height = BitConverter.ToInt32(header, 12);
            int width = BitConverter.ToInt32(header, 16);

            int mipMapCount = BitConverter.ToInt32(header, 28);
            uint pixelFormatFlags = BitConverter.ToUInt32(header, 80);
            uint fourCC = BitConverter.ToUInt32(header, 84);
            uint BitCount = BitConverter.ToUInt32(header, 88); // Get bitdepth

            TextureFormat format;

            if ((pixelFormatFlags & 0x4) != 0) // DDPF_FOURCC
            {
                if (fourCC == 0x31545844) // 'DXT1'
                {
                    format = TextureFormat.DXT1;
                }
                else if (fourCC == 0x35545844) // 'DXT5'
                {
                    format = TextureFormat.DXT5;
                }
                else
                {
                    throw new Exception("Unsupported DDS texture format");
                }
            }
            else if ((pixelFormatFlags & 0x40) != 0 && fourCC == 0) // DDPF_ALPHAPIXELS (standard L8)
            {
                format = TextureFormat.Alpha8;
            }
            else if ((pixelFormatFlags & 0x20000) != 0 && fourCC == 0) // DDPF_FOURCC with no FourCC (alternate L8)
            {
                if (BitCount == 16)
                {
                    format = TextureFormat.R16;
                }
                else
                {
                    format = TextureFormat.R8;
                }
            }
            else
            {
                throw new Exception("Unsupported DDS texture format");
            }

            // Create the Texture2D with or without mipmaps based on the header
            return new TextureLoadData
            {
                width = width,
                height = height,
                format = format,
                mips = mipMapCount > 1,
                linear = linear,
                unreadable = markUnreadable
            };
        }
    }
    
    class InFlightPNGLoad : InFlightLoad
    {
        public override bool IsComplete => true;

        public static InFlightPNGLoad Start(string path, bool linear, bool unreadable)
        {
            Texture2D tex;
            tex = new Texture2D(2, 2, TextureFormat.ARGB32, true, linear);
            tex.LoadRawTextureData(File.ReadAllBytes(path));
            tex.Apply(true, unreadable);
            
            return new InFlightPNGLoad
            {
                path = path,
                texture = tex,
            };
        }

        protected override bool DoMoveNext() => false;

        protected override void DoComplete() { }
    }
    #endregion

    #region Texture Helpers
    // This reflects the actual creation flags in
    // https://github.com/Unity-Technologies/UnityCsReference/blob/59b03b8a0f179c0b7e038178c90b6c80b340aa9f/Runtime/Export/Graphics/GraphicsEnums.cs#L626
    //
    // Most of the extra ones here are completely undocumented.
    [Flags]
    enum TextureCreationFlags
    {
        None,
        MipChain = 1 << 0,
        DontInitializePixels = 1 << 2,
        DontDestroyTexture = 1 << 3,
        DontCreateSharedTextureData = 1 << 4,
        APIShareable = 1 << 5,
        Crunch = 1 << 6,
    }


    // This uses a bunch of undocumented flags to prevent unity from
    // initializing or sharing the texture before we apply it.
    static Texture2D CreateUninitializedTexture(in TextureLoadData data)
    {
        return CreateUninitializedTexture(data.width, data.height, data.format, data.mips, data.linear);
    }

    /// <summary>
    /// Create a <see cref="Texture2D"/> without initializing its data.
    /// </summary>
    static Texture2D CreateUninitializedTexture(
        int width,
        int height,
        TextureFormat format = TextureFormat.RGBA32,
        bool mipChain = false,
        bool linear = false
    )
    {
        // The code in here exactly matches the behaviour of the Texture2D
        // constructors which directly take a TextureFormat, with one
        // difference: it includes the DontInitializePixels flag.
        //
        // This is necessary because the Texture2D constructors that take
        // GraphicsFormat validate the format differently than those that take
        // TextureFormat, and only the GraphicsFormat constructors allow you to
        // pass TextureCreationFlags.
        //
        // I (@Phantomical) have taken at look at decompiled implementation for
        // Internal_Create_Impl and validated that this works as you would expect.

        var tex = (Texture2D)FormatterServices.GetUninitializedObject(typeof(Texture2D));
        if (!tex.ValidateFormat(format))
            return tex;

        var gformat = GraphicsFormatUtility.GetGraphicsFormat(format, isSRGB: !linear);
        var flags = TextureCreationFlags.DontInitializePixels;
        int mipCount = !mipChain ? 1 : -1;

        if (mipCount != 1)
            flags |= TextureCreationFlags.MipChain;
        if (GraphicsFormatUtility.IsCrunchFormat(format))
            flags |= TextureCreationFlags.Crunch;

        var uflags = (UnityEngine.Experimental.Rendering.TextureCreationFlags)flags;
        Texture2D.Internal_Create(tex, width, height, mipCount, gformat, uflags, IntPtr.Zero);
        return tex;
    }

    // This takes care of completing the read and then disposing of it
    // appropriately, which saves us a whole bunch of try-catch blocks.
    readonly struct SafeReadHandle(ReadHandle handle) : IDisposable
    {
        readonly ReadHandle handle = handle;

        public ReadStatus Status => handle.Status;
        public JobHandle JobHandle => handle.JobHandle;

        public void Dispose()
        {
            if (!handle.IsValid())
                return;
            if (handle.Status == ReadStatus.InProgress)
                handle.JobHandle.Complete();
            
            handle.Dispose();
        }
    }

    struct CopyJob : IJob
    {
        [ReadOnly]
        [DeallocateOnJobCompletion]
        public NativeArray<byte> input;

        [WriteOnly]
        [DeallocateOnJobCompletion]
        public NativeArray<byte> output;

        public readonly void Execute()
        {
            output.Slice().CopyFrom(input);
        }
    }
    #endregion

    /// <summary>
    /// A class that allows you to conditionally dispose of a value over a
    /// region. Using statements make their variable readonly, so this class
    /// provides a indirection that allows you to move the inner value out.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    class DisposeSlot<T> : IDisposable
        where T : IDisposable
    {
        public T Value = default;

        public T Take()
        {
            var value = Value;
            Value = default;
            return value;
        }

        public void Dispose()
        {
            Value?.Dispose();
            Value = default;
        }
    }
}
