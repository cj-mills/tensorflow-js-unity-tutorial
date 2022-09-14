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
    public static extern void SetTFJSBackend(string backend);

    [DllImport("__Internal")]
    public static extern void InitTFJSModel(string model_path, float[] mean, float[] std_dev, float[] output_data, int output_size);

    [DllImport("__Internal")]
    public static extern bool PerformInference(byte[] image_data, int size, int width, int height);
}
