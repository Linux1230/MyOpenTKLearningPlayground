using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Common.Input;
using Image = SixLabors.ImageSharp.Image;
using System.Runtime.InteropServices;
using OpenTK.Mathematics;
using OpenTKLearning;
using OpenTK.Graphics.OpenGL4;

// https://www.youtube.com/watch?v=wFnt6fOX97U

try
{
    var gameWindow = CreateWindow();
    gameWindow.Load += WindowLoaded;
    gameWindow.RenderFrame += (frameEventArgs) => RenderFrame(frameEventArgs, ref gameWindow);
    gameWindow.Run();
}
catch (Exception ex)
{
    Console.WriteLine(ex);
}

static void RenderFrame(FrameEventArgs frameEventArgs, ref GameWindow gameWindow)
{
    GL.Clear(ClearBufferMask.ColorBufferBit);
    var vao = CreateVao(out var indexBuffer);
    GL.BindBuffer(BufferTarget.ArrayBuffer, indexBuffer);
    GL.BindVertexArray(vao);
    
    GL.DrawElements(PrimitiveType.Triangles, 3, DrawElementsType.UnsignedInt, IntPtr.Zero);

    GL.BindVertexArray(0);
    GL.BindBuffer(BufferTarget.ElementArrayBuffer, 0);
    GL.DeleteBuffer(indexBuffer);
    GL.DeleteVertexArray(vao);

    gameWindow.SwapBuffers();
}

static void WindowLoaded()
{
    var program = CreateShader();
    GL.UseProgram(program);
}

static int CreateVao(out int indexBuffer)
{
    var vertexBuffer = GL.GenBuffer();
    var colorBuffer = GL.GenBuffer();
    indexBuffer = GL.GenBuffer();
    var vao = GL.GenVertexArray();

    GL.BindVertexArray(vao);

    var arraySize = 9;
    var bufferSize = arraySize * sizeof(float);

    var a = new float[] { -0.5f, -0.5f, 0.0f };
    var b = new float[] { 0.5f, -0.5f, 0.0f };
    var c = new float[] { 0.0f, 0.5f, 0, 0f };
    var triangle = new float[arraySize];
    Array.Copy(a, 0, triangle, 0, a.Length);
    Array.Copy(b, 0, triangle, a.Length, b.Length);
    Array.Copy(c, 0, triangle, a.Length + b.Length, Math.Min(c.Length, arraySize - c.Length - c.Length));

    GL.BindBuffer(BufferTarget.ArrayBuffer, vertexBuffer);
    GL.BufferData(BufferTarget.ArrayBuffer, bufferSize, triangle, BufferUsageHint.StaticCopy);
    GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 0, 0);
    GL.EnableVertexAttribArray(0);

    var colorA = new float[] { 1.0f, 0.0f, 0.0f };
    var colorB = new float[] { 0.0f, -0.0f, 1.0f };
    var colorC = new float[] { 0.0f, 1.0f, 0.0f };
    var colors = new float[arraySize];
    Array.Copy(colorA, 0, colors, 0, colorA.Length);
    Array.Copy(colorB, 0, colors, colorA.Length, colorB.Length);
    Array.Copy(colorC, 0, colors, colorA.Length + colorB.Length, Math.Min(colorC.Length, arraySize - colorA.Length - colorB.Length));

    GL.BindBuffer(BufferTarget.ArrayBuffer, colorBuffer);
    GL.BufferData(BufferTarget.ArrayBuffer, bufferSize, colors, BufferUsageHint.StaticCopy);
    GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, 0, 0);
    GL.EnableVertexAttribArray(1);

    GL.BindBuffer(BufferTarget.ElementArrayBuffer, indexBuffer);
    GL.BufferData(BufferTarget.ElementArrayBuffer, 3 * sizeof(uint), new uint[] { 0, 1, 2 }, BufferUsageHint.StaticCopy);

    GL.BindVertexArray(0);
    GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
    GL.DeleteBuffer(vertexBuffer);
    GL.DeleteBuffer(colorBuffer);

    return vao;
}

static int CreateShader()
{
    var vShader = GL.CreateShader(ShaderType.VertexShader);
    var fShader = GL.CreateShader(ShaderType.FragmentShader);
    GL.ShaderSource(vShader, ReadTextFileFromAssets("vertex_shader.glsl"));
    GL.ShaderSource(fShader, ReadTextFileFromAssets("fragment_shader.glsl"));

    GL.CompileShader(vShader);
    GL.CompileShader(fShader);

    Console.WriteLine(GL.GetShaderInfoLog(vShader));
    Console.WriteLine(GL.GetShaderInfoLog(fShader));

    var program = GL.CreateProgram();
    GL.AttachShader(program, vShader);
    GL.AttachShader(program, fShader);
    GL.LinkProgram(program);
    GL.DetachShader(program, vShader);
    GL.DetachShader(program, fShader);
    GL.DeleteShader(vShader);
    GL.DeleteShader(fShader);

    Console.WriteLine(GL.GetProgramInfoLog(program));

    return program;
}

static GameWindow CreateWindow()
{
    if (!Version.TryParse("4.1", out var apiVersion))
    {
        throw new Exception("Failed to parse api version!");
    }

    var windowSize = new Vector2i(512, 512);
    var monitorInfo = Monitors.GetPrimaryMonitor();
    var windowCenter =
        new Vector2i((monitorInfo.HorizontalResolution - windowSize.X) / 2,
                     (monitorInfo.VerticalResolution - windowSize.Y) / 2);

    var nativeWindowSettings = new NativeWindowSettings()
    {
        Icon = LoadIconFromAssets("icon.png"),
        IsEventDriven = false,
        API = ContextAPI.OpenGL,
        APIVersion = apiVersion,
        AutoLoadBindings = true,
        CurrentMonitor = monitorInfo.Handle,
        Location = windowCenter,
        Size = windowSize,
        StartFocused = true,
        StartVisible = true,
        Title = "OpenTK Learning",
        WindowBorder = WindowBorder.Resizable,
        WindowState = WindowState.Normal
    };

    var gameWindowSettings = new GameWindowSettings
    {
        IsMultiThreaded = false,
        RenderFrequency = 60d,
        UpdateFrequency = 60d
    };

    return new GameWindow(gameWindowSettings, nativeWindowSettings);
}

static WindowIcon LoadIconFromAssets(string fileName)
{
    var image = Image.Load<Rgba32>(Path.Combine(GlobalPaths.Assets, fileName));
    if (!image.DangerousTryGetSinglePixelMemory(out var pixelMemory))
    {
        throw new Exception("Failed to get pixel memory!");
    }
    var pixels = MemoryMarshal.AsBytes(pixelMemory.Span).ToArray();
    var windowIcon = new WindowIcon(new OpenTK.Windowing.Common.Input.Image(225, 225, pixels));
    return windowIcon;
}

static string ReadTextFileFromAssets(string fileName) =>
    File.ReadAllText(Path.Combine(GlobalPaths.Assets, fileName));
