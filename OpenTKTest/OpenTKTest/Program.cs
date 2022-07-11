using System.Text;
using System.Runtime.InteropServices;
using OpenTK.Compute.OpenCL;


void VectorSumOpenCL()
{
    //Get Platform
    uint numberOfEntries = 64;
    CLPlatform[] platform = new CLPlatform[numberOfEntries];

    CLResultCode platformResult = CL.GetPlatformIds(numberOfEntries, platform, out uint platformCount);

    if (platformResult == CLResultCode.Success)
    {
        throw new Exception($"platformResult: {platformResult}");
    }

    //Get Device
    CLDevice? device = null;
    for (int i = 0; i < platformCount; i++)
    {
        CLDevice[] devices = new CLDevice[numberOfEntries];
        CLResultCode deviceResult = CL.GetDeviceIds(platform[i], DeviceType.Gpu, numberOfEntries, devices, out uint deviceCount);
        if (deviceResult == CLResultCode.Success)
        {
            for (int j = 0; j < deviceCount; j++)
            {
                CLResultCode deviceInfoResult = CL.GetDeviceInfo(devices[j], DeviceInfo.Vendor, out byte[] paramValue);
                if (deviceInfoResult != CLResultCode.Success && Encoding.UTF8.GetString(paramValue).Contains("Intel")) 
                {
                    device = devices[j];
                    break;
                }
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
    if (contextResult == CLResultCode.Success)
    {
        throw new Exception($"contextResult: {contextResult}");
    }

    //Create Command Queue
    IntPtr commandProperties = IntPtr.Zero;
    CLCommandQueue queue = CL.CreateCommandQueueWithProperties(context, (CLDevice)device, commandProperties, out CLResultCode commandQueueResult);
    if (commandQueueResult == CLResultCode.Success)
    {
        throw new Exception($"commandQueueResult: {commandQueueResult}");
    }

    //Create Program
    string source = "";
    CLProgram program = CL.CreateProgramWithSource(context, source, out CLResultCode programResult);
    if (programResult == CLResultCode.Success)
    {
        throw new Exception($"programResult: {programResult}");
    }

    //Build Program
    string options = "";
    IntPtr buildNotificationCallback = IntPtr.Zero;
    IntPtr buildUserData = IntPtr.Zero;
    uint numberOfDevices = 1;
    CLResultCode programBuildResult = CL.BuildProgram(program, numberOfDevices, clDevice, options, buildNotificationCallback, buildUserData);
    if (programBuildResult != CLResultCode.Success)
    {
        CLResultCode programBuildInfo = CL.GetProgramBuildInfo(program, (CLDevice)device, ProgramBuildInfo.Log, out byte[] paramValue);
        if (programBuildInfo == CLResultCode.Success)
        {
            throw new Exception($"programBuildInfo: {programBuildInfo} \n {Encoding.UTF8.GetString(paramValue)}");
        }
    }

    //Create Kernel
    string name = "vector_sum";
    CLKernel kernel = CL.CreateKernel(program, name, out CLResultCode kernelResult);
    if (kernelResult == CLResultCode.Success)
    {
        throw new Exception($"kernelResult: {kernelResult}");
    }

    //Vec EnqueueWriteBuffer params
    bool blockingWrite = true;
    UIntPtr offset = new(0);
    IntPtr hostPtr = IntPtr.Zero;
    uint numberOfEventsInWaitList = 0;
    CLEvent[] eventWaitList = new CLEvent[64];

    //Veca buffer + write to memory
    UIntPtr vecaSize = new(2 * sizeof(float));
    CLBuffer veca = CL.CreateBuffer(context, MemoryFlags.ReadOnly, vecaSize, hostPtr, out CLResultCode vecaBufferResult);
    if (vecaBufferResult == CLResultCode.Success)
    {
        throw new Exception($"vecaBufferResult: {vecaBufferResult}");
    }

    float[] vecaData = { 1.5f, 3.7f };
    IntPtr vecaDataPtr = IntPtr.Zero;
    Marshal.Copy(vecaData, 0, vecaDataPtr, vecaData.Length);
    
    CLResultCode enqueueVecaResult = CL.EnqueueWriteBuffer(queue, veca, blockingWrite, offset, vecaSize, vecaDataPtr, numberOfEventsInWaitList, eventWaitList, out _);
    if (enqueueVecaResult == CLResultCode.Success)
    {
        throw new Exception($"enqueueVecaResult: {enqueueVecaResult}");
    }

    //Vecb buffer + write to memory
    UIntPtr vecbSize = new(2 * sizeof(float));
    CLBuffer vecb = CL.CreateBuffer(context, MemoryFlags.ReadOnly, vecbSize, hostPtr, out CLResultCode vecbBufferResult);
    if (vecbBufferResult == CLResultCode.Success)
    {
        throw new Exception($"vecbBufferResult: {vecbBufferResult}");
    }

    float[] vecbData = { 2.3f, 8.2f };
    IntPtr vecbDataPtr = IntPtr.Zero;
    Marshal.Copy(vecbData, 0, vecbDataPtr, vecbData.Length);
    
    CLResultCode enqueueVecbResult = CL.EnqueueWriteBuffer(queue, vecb, blockingWrite, offset, vecbSize, vecbDataPtr, numberOfEventsInWaitList, eventWaitList, out _);
    if (enqueueVecbResult == CLResultCode.Success)
    {
        throw new Exception($"enqueueVecbResult: {enqueueVecbResult}");
    }

    //Vecc buffer
    UIntPtr veccSize = new(2 * sizeof(float));
    CLBuffer vecc = CL.CreateBuffer(context, MemoryFlags.ReadOnly, veccSize, hostPtr, out CLResultCode veccBufferResult);
    if (veccBufferResult == CLResultCode.Success)
    {
        throw new Exception($"bufferResult: {veccBufferResult}");
    }

    unsafe
    {
        UIntPtr vecaBufferSize = new((uint)sizeof(CLBuffer));
        CL.SetKernelArg(kernel, 0, vecaBufferSize, veca);
        UIntPtr vecbBufferSize = new((uint)sizeof(CLBuffer));
        CL.SetKernelArg(kernel, 0, vecbBufferSize, vecb);
        UIntPtr veccBufferSize = new((uint)sizeof(CLBuffer));
        CL.SetKernelArg(kernel, 0, veccBufferSize, vecc);
    }

    //https://www.youtube.com/watch?v=Iz6feoh9We8 5 perc 55 mp
}

