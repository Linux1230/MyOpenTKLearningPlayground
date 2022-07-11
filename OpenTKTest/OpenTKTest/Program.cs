using System.Text;
using System.Runtime.InteropServices;
using OpenTK.Compute.OpenCL;

try
{
    VectorSumOpenCL();
    Console.ReadKey();
}
catch (Exception ex)
{
    Console.WriteLine(ex.Message);
}

static bool CompareVendor(byte[] paramValue, string vendorName) =>
    Encoding.UTF8.GetString(paramValue).Contains(vendorName, StringComparison.InvariantCultureIgnoreCase);

//Reads the given file
static string ReadProgramFile(string fileName)
{
    string? source = File.ReadAllText(fileName);
    if (string.IsNullOrEmpty(source))
        throw new FileNotFoundException("File not Found!", fileName);

    return source;
}

//Throws an exception with the given parameters, if result is not equals CLResultCode.Success
static void ThrowError(CLResultCode result, string name, string error = "")
{
    if (result == CLResultCode.Success)
        return;

    throw new Exception($"{name}: {result} \n error: {error}");
}

//https://www.youtube.com/watch?v=Iz6feoh9We8 implemented in C#
static void VectorSumOpenCL()
{
    //Get Platform
    uint numberOfEntries = 64;
    CLPlatform[] platform = new CLPlatform[numberOfEntries];

    CLResultCode platformResult = CL.GetPlatformIds(numberOfEntries, platform, out uint platformCount);
    ThrowError(platformResult, nameof(platformResult));

    //Get Device
    CLDevice? device = null;
    for (int i = 0; i < platformCount; i++)
    {
        CLDevice[] devices = new CLDevice[numberOfEntries];
        CLResultCode deviceResult = CL.GetDeviceIds(platform[i], DeviceType.Gpu, numberOfEntries, devices, out uint deviceCount);
        ThrowError(deviceResult, nameof(deviceResult));

        for (int j = 0; j < deviceCount; j++)
        {
            CLResultCode deviceInfoResult = CL.GetDeviceInfo(devices[j], DeviceInfo.Vendor, out byte[] paramValue);
            ThrowError(deviceInfoResult, nameof(deviceInfoResult));

            if (CompareVendor(paramValue, "Intel"))
            {
                device = devices[j];
                break;
            }
            else if (CompareVendor(paramValue, "AMD"))
            {
                device = devices[j];
                break;
            }
            else if (CompareVendor(paramValue, "NVIDIA"))
            {
                device = devices[j];
                break;
            }
        }
    }

    if (device is null)
        return;

    //Setup context
    IntPtr contextProperties = IntPtr.Zero;
    IntPtr contextNotificationCallback = IntPtr.Zero;
    IntPtr contextUserData = IntPtr.Zero;
    CLDevice[] clDevice = new CLDevice[1];
    clDevice[0] = (CLDevice)device;
    CLContext context = CL.CreateContext(contextProperties, clDevice, contextNotificationCallback, contextUserData, out CLResultCode contextResult);
    ThrowError(contextResult, nameof(contextResult));

    //Create Command Queue
    IntPtr commandProperties = IntPtr.Zero;
    CLCommandQueue queue = CL.CreateCommandQueueWithProperties(context, (CLDevice)device, commandProperties, out CLResultCode commandQueueResult);
    ThrowError(commandQueueResult, nameof(commandQueueResult));

    //Create Program
    string source = ReadProgramFile("vecsum.cl");
    CLProgram program = CL.CreateProgramWithSource(context, source, out CLResultCode programResult);
    ThrowError(programResult, nameof(programResult));

    //Build Program
    string options = "";
    IntPtr buildNotificationCallback = IntPtr.Zero;
    IntPtr buildUserData = IntPtr.Zero;
    uint numberOfDevices = 1;
    CLResultCode programBuildResult = CL.BuildProgram(program, numberOfDevices, clDevice, options, buildNotificationCallback, buildUserData);
    if (programBuildResult != CLResultCode.Success)
    {
        CLResultCode programBuildInfo = CL.GetProgramBuildInfo(program, (CLDevice)device, ProgramBuildInfo.Log, out byte[] paramValue);
        ThrowError(programBuildInfo, nameof(programBuildInfo), Encoding.UTF8.GetString(paramValue));
    }

    //Create Kernel
    string name = "vector_sum";
    CLKernel kernel = CL.CreateKernel(program, name, out CLResultCode kernelResult);
    ThrowError(kernelResult, nameof(kernelResult));

    //Reusable params
    uint numberOfEventsInWaitList = 0;
    //CLEvent[] eventWaitList = new CLEvent[64];
    uint vecArrayLenght = 256;

    //Vec EnqueueWriteBuffer params
    bool blockingWrite = true;
    UIntPtr offset = UIntPtr.Zero;
    IntPtr hostPtr = IntPtr.Zero;

    //Veca buffer
    UIntPtr vecaSize = new(vecArrayLenght * sizeof(float));
    CLBuffer veca = CL.CreateBuffer(context, MemoryFlags.ReadOnly, vecaSize, hostPtr, out CLResultCode vecaBufferResult);
    ThrowError(vecaBufferResult, nameof(vecaBufferResult));

    //Vecb buffer
    UIntPtr vecbSize = new(vecArrayLenght * sizeof(float));
    CLBuffer vecb = CL.CreateBuffer(context, MemoryFlags.ReadOnly, vecbSize, hostPtr, out CLResultCode vecbBufferResult);
    ThrowError(vecbBufferResult, nameof(vecbBufferResult));

    //Create the data arrays
    float[] vecaData = new float[vecArrayLenght];
    float[] vecbData = new float[vecArrayLenght];

    //Fill up the data arrays
    for (int i = 0; i < vecArrayLenght; i++)
    {
        vecaData[i] = i * i;
        vecbData[i] = i;
    }

    //Create veca ptr
    IntPtr vecaDataPtr = Marshal.AllocHGlobal((int)vecaSize);
    Marshal.Copy(vecaData, 0, vecaDataPtr, vecaData.Length);

    //Create vecb ptr
    IntPtr vecbDataPtr = Marshal.AllocHGlobal((int)vecbSize);
    Marshal.Copy(vecbData, 0, vecbDataPtr, vecbData.Length);

    //veca write
    CLResultCode enqueueVecaResult = CL.EnqueueWriteBuffer(queue, veca, blockingWrite, offset, vecaSize, vecaDataPtr, numberOfEventsInWaitList, null /*eventWaitList*/, out _);
    ThrowError(enqueueVecaResult, nameof(enqueueVecaResult));

    //vecb write
    CLResultCode enqueueVecbResult = CL.EnqueueWriteBuffer(queue, vecb, blockingWrite, offset, vecbSize, vecbDataPtr, numberOfEventsInWaitList, null /*eventWaitList*/, out _);
    ThrowError(enqueueVecbResult, nameof(enqueueVecbResult));

    //Vecc buffer
    UIntPtr veccSize = new(vecArrayLenght * sizeof(float));
    IntPtr veccDataPtr = Marshal.AllocHGlobal((int)veccSize);
    CLBuffer vecc = CL.CreateBuffer(context, MemoryFlags.ReadOnly, veccSize, hostPtr, out CLResultCode veccBufferResult);
    ThrowError(veccBufferResult, nameof(veccBufferResult));

    //Set vec kernel params
    CLResultCode kernelResult1 = CL.SetKernelArg(kernel, 0, veca);
    ThrowError(kernelResult1, nameof(kernelResult1));
    CLResultCode kernelResult2 = CL.SetKernelArg(kernel, 1, vecb);
    ThrowError(kernelResult2, nameof(kernelResult2));
    CLResultCode kernelResult3 = CL.SetKernelArg(kernel, 2, vecc);
    ThrowError(kernelResult3, nameof(kernelResult3));

    //Setup kernel queue
    uint workDimension = 1;
    UIntPtr[] globalWorkOffset = new UIntPtr[1];
    globalWorkOffset[0] = new UIntPtr(0);
    UIntPtr[] globalWorkSize = new UIntPtr[1];
    globalWorkSize[0] = new UIntPtr(256);
    UIntPtr[] localWorkSize = new UIntPtr[1];
    localWorkSize[0] = new UIntPtr(64);
    CLResultCode enqueueKernelResult = CL.EnqueueNDRangeKernel(queue, kernel, workDimension, globalWorkOffset, globalWorkSize, localWorkSize, numberOfEventsInWaitList, null /*eventWaitList*/, out _);
    ThrowError(enqueueKernelResult, nameof(enqueueKernelResult));

    //Read vecc
    CLResultCode enqueueReadBuffer = CL.EnqueueReadBuffer(queue, vecc, blockingWrite, offset, veccSize, veccDataPtr, numberOfEventsInWaitList, null /*eventWaitList*/, out _);
    ThrowError(enqueueReadBuffer, nameof(enqueueReadBuffer));

    //Pass queued up commands to device
    CL.Finish(queue);

    //get c# array from veccDataPtr
    float[] veccData = new float[vecArrayLenght];
    Marshal.Copy(veccDataPtr, veccData, 0, (int)vecArrayLenght);

    //write result to console
    Console.WriteLine($"Result: ");
    foreach (var item in veccData)
    {
        Console.WriteLine($"veccData: {item}");
    }
    Console.WriteLine();

    //free up used memory by OpenCL
    CL.ReleaseMemoryObject(veca);
    CL.ReleaseMemoryObject(vecb);
    CL.ReleaseMemoryObject(vecc);
    CL.ReleaseKernel(kernel);
    CL.ReleaseProgram(program);
    CL.ReleaseCommandQueue(queue);
    CL.ReleaseContext(context);
    CL.ReleaseDevice((CLDevice)device);

    //free up used memory by C#
    Marshal.FreeHGlobal(contextProperties);
    Marshal.FreeHGlobal(contextNotificationCallback);
    Marshal.FreeHGlobal(contextUserData);
    Marshal.FreeHGlobal(commandProperties);
    Marshal.FreeHGlobal(buildNotificationCallback);
    Marshal.FreeHGlobal(buildUserData);
    unsafe
    {
        Marshal.FreeHGlobal((IntPtr)offset.ToPointer());
    }
    Marshal.FreeHGlobal(hostPtr);
    unsafe
    {
        Marshal.FreeHGlobal((IntPtr)vecaSize.ToPointer());
        Marshal.FreeHGlobal((IntPtr)vecbSize.ToPointer());
    }
    Marshal.FreeHGlobal(vecaDataPtr);
    Marshal.FreeHGlobal(vecbDataPtr);
    Marshal.FreeHGlobal(veccDataPtr);
    unsafe
    {
        Marshal.FreeHGlobal((IntPtr)veccSize.ToPointer());
        Marshal.FreeHGlobal((IntPtr)globalWorkOffset[0].ToPointer());
        Marshal.FreeHGlobal((IntPtr)globalWorkSize[0].ToPointer());
        Marshal.FreeHGlobal((IntPtr)localWorkSize[0].ToPointer());
    }
    GC.Collect();
}
