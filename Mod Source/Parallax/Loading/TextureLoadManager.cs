using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.IO.LowLevel.Unsafe;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace Parallax.Loading;

[KSPAddon(KSPAddon.Startup.MainMenu, once: true)]
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

    LoadHandle DoLoadTextureAsync(string path, bool linear, bool unreadable = true)
    {
        ParallaxDebug.Log("Loading Parallax Texture: " + path);

        TaskCompletionSource<Texture2D> completion = new();
        Task<Texture2D> task = completion.Task;

        try
        {
            string filePath = ConfigLoader.GameDataPath + path;
            InFlightLoad load;

            if (path.EndsWith(".dds"))
                load = InFlightDDSLoad.Start(filePath, linear, unreadable);
            else
                load = InFlightPNGLoad.Start(filePath, linear, unreadable);

            load.completion = completion;
            load.path = path;
            if (load.MoveNext())
                inFlight.Add(load);

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

        State state;
        SafeReadHandle handle;
        TextureLoadData textureData;
        NativeArray<byte> fileData;
        NativeArray<byte> rawTextureData;
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
            var format = textureData.format;
            bool useRawData = format == TextureFormat.DXT5
                || format == TextureFormat.DXT1
                || format == TextureFormat.R8
                || format == TextureFormat.Alpha8
                || format == TextureFormat.R16;
            bool gpuOnly = useRawData && textureData.unreadable;

            var inFlight = new InFlightDDSLoad()
            {
                path = path,
                texture = CreateUninitializedTexture(in textureData, gpuOnly),
                textureData = textureData,
                fileData = new NativeArray<byte>(
                    (int)(length - DDS_HEADER_SIZE),
                    // Specifically use TempJob here because Allocator.Temp is a bump
                    // allocator and these are large allocations.
                    Allocator.TempJob,
                    NativeArrayOptions.UninitializedMemory
                )
            };

            ReadCommand command = new()
            {
                Buffer = inFlight.fileData.GetUnsafePtr(),
                Offset = DDS_HEADER_SIZE,
                Size = length - DDS_HEADER_SIZE
            };
            inFlight.handle = new SafeReadHandle(AsyncReadManager.Read(path, &command, 1));
            inFlight.job = inFlight.handle.JobHandle;
            inFlight.rawTextureData = inFlight.texture.GetRawTextureData<byte>();

            Debug.Log($"texData:  {inFlight.texture.GetRawTextureData<byte>().Length:X}");
            Debug.Log($"rawData:  {inFlight.fileData.Length:X}");

            var copyJob = new CopyJob
            {
                input = inFlight.fileData,
                output = inFlight.texture.GetRawTextureData<byte>()
            };
            inFlight.job = copyJob.Schedule(inFlight.job);

            inFlight.state = State.WaitingOnJob;
            return inFlight;
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
            fileData.Dispose();
            rawTextureData.Dispose();
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
    static Texture2D CreateUninitializedTexture(in TextureLoadData data, bool gpuOnly)
    {
        var flags = TextureCreationFlags.DontInitializePixels;
        if (data.mips)
            flags |= TextureCreationFlags.MipChain;
        if (GraphicsFormatUtility.IsCrunchFormat(data.format))
            flags |= TextureCreationFlags.Crunch;
        // if (gpuOnly)
        //     flags |= TextureCreationFlags.DontCreateSharedTextureData;

        var format = GraphicsFormatUtility.GetGraphicsFormat(data.format, !data.linear);
        var uflags = (UnityEngine.Experimental.Rendering.TextureCreationFlags)flags;
        
        return new Texture2D(data.width, data.height, format, uflags);
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
        public NativeArray<byte> input;

        [WriteOnly]
        public NativeArray<byte> output;

        public void Execute()
        {
            output.Slice().CopyFrom(input);
        }
    }
    #endregion
}
