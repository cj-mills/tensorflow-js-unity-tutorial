using System;
using System.Runtime.InteropServices;

/// <summary>
/// Class with a JS Plugin functions for WebGL.
/// </summary>
public static class WebGLPluginJS
{
    // Importing "GetExternalJS"
    [DllImport("__Internal")]
    public static extern void GetExternalJS();

    [DllImport("__Internal")]
    public static extern void InitTFJSModel(string model_path, string backend, float[] mean, float[] std_dev);

    [DllImport("__Internal")]
    public static extern int PerformInference(byte[] array, int size, int width, int height);
}
