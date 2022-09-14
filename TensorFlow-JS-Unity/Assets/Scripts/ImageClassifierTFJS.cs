using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.UI;
using System.IO;
using UnityEngine.Networking;


#if UNITY_EDITOR
using UnityEditor;

[InitializeOnLoad]
public class Startup
{
    [System.Serializable]
    class ModelData
    {
        public string name;
        public string path;

        public ModelData(string name, string path)
        {
            this.name = name;
            this.path = path;
        }
    }

    [System.Serializable]
    class ModelList
    {
        public List<ModelData> models;

        public ModelList(List<ModelData> models)
        {
            this.models = models;
        }
    }

    static Startup()
    {
        string tfjsModelsDir = "TFJSModels";
        List<ModelData> models = new List<ModelData>();

        Debug.Log("Available models");
        // Get the paths for each model folder
        foreach (string dir in Directory.GetDirectories($"{Application.streamingAssetsPath}/{tfjsModelsDir}"))
        {
            string dirStr = dir.Replace("\\", "/");
            // Extract the model folder name
            string[] splits = dirStr.Split('/');
            string modelName = splits[splits.Length - 1];

            // Get the paths for the model.json file for each model
            foreach (string file in Directory.GetFiles(dirStr))
            {
                if (file.EndsWith("model.json"))
                {
                    string fileStr = file.Replace("\\", "/").Replace(Application.streamingAssetsPath, "");
                    models.Add(new ModelData(modelName, fileStr));
                }
            }
        }
        ModelList modelList = new ModelList(models);
        string json = JsonUtility.ToJson(modelList);
        Debug.Log($"Model List JSON: {json}");
        using StreamWriter writer = new StreamWriter($"{Application.streamingAssetsPath}/models.json");
        writer.Write(json);
    }
}
#endif

public class ImageClassifierTFJS : MonoBehaviour
{
    [Header("Scene Objects")]
    [Tooltip("The Screen object for the scene")]
    public Transform screen;

    [Header("Data Processing")]
    [Tooltip("The target minimum model input dimensions")]
    public int targetDim = 216;

    [Header("Output Processing")]
    [Tooltip("A json file containing the class labels")]
    public TextAsset classLabels;

    [Header("Debugging")]
    [Tooltip("Print debugging messages to the console")]
    public bool printDebugMessages = true;

    [Header("Webcam")]
    [Tooltip("Use a webcam as input")]
    public bool useWebcam = false;
    [Tooltip("The requested webcam dimensions")]
    public Vector2Int webcamDims = new Vector2Int(1280, 720);
    [Tooltip("The requested webcam framerate")]
    [Range(0, 60)]
    public int webcamFPS = 60;

    [Header("GUI")]
    [Tooltip("Display predicted class")]
    public bool displayPredictedClass = true;
    [Tooltip("Display fps")]
    public bool displayFPS = true;
    [Tooltip("The on-screen text color")]
    public Color textColor = Color.red;
    [Tooltip("The scale value for the on-screen font size")]
    [Range(0, 99)]
    public int fontScale = 50;
    [Tooltip("The number of seconds to wait between refreshing the fps value")]
    [Range(0.01f, 1.0f)]
    public float fpsRefreshRate = 0.1f;
    [Tooltip("The toggle for using a webcam as the input source")]
    public Toggle useWebcamToggle;
    [Tooltip("The dropdown menu that lists available webcam devices")]
    public Dropdown webcamDropdown;
    [Tooltip("The dropdown menu that lists available TFJS models")]
    public Dropdown modelDropdown;
    [Tooltip("The dropdown menu that lists available TFJS backends")]
    public Dropdown backendDropdown;

    [Header("TFJS")]
    [Tooltip("The name of the TFJS models folder")]
    public string tfjsModelsDir = "TFJSModels";
    [Tooltip("A list json files containing the normalization stats for available models")]
    public TextAsset[] normalizationStatsList;

    // List of available webcam devices
    private WebCamDevice[] webcamDevices;
    // Live video input from a webcam
    private WebCamTexture webcamTexture;
    // The name of the current webcam  device
    private string currentWebcam;

    // The test image dimensions
    private Vector2Int imageDims;
    // The test image texture
    private Texture imageTexture;
    // The current screen object dimensions
    private Vector2Int screenDims;
    // The model GPU input texture
    private RenderTexture inputTextureGPU;
    // The model CPU input texture
    private Texture2D inputTextureCPU;

    // A class for reading in class labels from a JSON file
    class ClassLabels { public string[] classes; }
    // The ordered list of class names
    private string[] classes;
    // 
    private bool isInitialized;

    // The current frame rate value
    private int fps = 0;
    // Controls when the frame rate value updates
    private float fpsTimer = 0f;

    // File paths for the available TFJS models
    private List<string> modelPaths = new List<string>();
    // Names of the available TFJS models
    private List<string> modelNames = new List<string>();
    // Names of the available TFJS backends
    //private List<string> tfjsBackends = new List<string>();
    private List<string> tfjsBackends = new List<string> { "webgl" };


    float[] output_data = new float[2];


    // A class for reading in normalization stats from a JSON file
    class NormalizationStats { public float[] mean; public float[] std; }
    float[] mean = new float[3];
    float[] std = new float[3];

    [System.Serializable]
    class ModelData { public string name; public string path; }
    [System.Serializable]
    class ModelList { public List<ModelData> models; }

    /// <summary>
    /// Initialize the selected webcam device
    /// </summary>
    /// <param name="deviceName">The name of the selected webcam device</param>
    private void InitializeWebcam(string deviceName)
    {
        // Stop any webcams already playing
        if (webcamTexture && webcamTexture.isPlaying) webcamTexture.Stop();

        // Create a new WebCamTexture
        webcamTexture = new WebCamTexture(deviceName, webcamDims.x, webcamDims.y, webcamFPS);

        // Start the webcam
        webcamTexture.Play();
        // Check if webcam is playing
        useWebcam = webcamTexture.isPlaying;
        // Update toggle value
        useWebcamToggle.SetIsOnWithoutNotify(useWebcam);

        Debug.Log(useWebcam ? "Webcam is playing" : "Webcam not playing, option disabled");
    }


    /// <summary>
    /// Resize and position an in-scene screen object
    /// </summary>
    private void InitializeScreen()
    {
        // Set the texture for the screen object
        screen.gameObject.GetComponent<MeshRenderer>().material.mainTexture = useWebcam ? webcamTexture : imageTexture;
        // Set the screen dimensions
        screenDims = useWebcam ? new Vector2Int(webcamTexture.width, webcamTexture.height) : imageDims;

        // Flip the screen around the Y-Axis when using webcam
        float yRotation = useWebcam ? 180f : 0f;
        // Invert the scale value for the Z-Axis when using webcam
        float zScale = useWebcam ? -1f : 1f;

        // Set screen rotation
        screen.rotation = Quaternion.Euler(0, yRotation, 0);
        // Adjust the screen dimensions
        screen.localScale = new Vector3(screenDims.x, screenDims.y, zScale);

        // Adjust the screen position
        screen.position = new Vector3(screenDims.x / 2, screenDims.y / 2, 1);
    }


    /// <summary>
    /// 
    /// </summary>
    /// <param name="json"></param>
    private void GetTFJSModels(string json)
    {
        ModelList modelList = JsonUtility.FromJson<ModelList>(json);
        foreach (ModelData model in modelList.models)
        {
            //Debug.Log($"{model.name}: {model.path}");
            modelNames.Add(model.name);
            string path = $"{Application.streamingAssetsPath}{model.path}";
            modelPaths.Add(path);
        }
        // Remove default dropdown options
        modelDropdown.ClearOptions();
        // Add TFJS model names to menu
        modelDropdown.AddOptions(modelNames);
        // Select the first option in the dropdown
        modelDropdown.SetValueWithoutNotify(0);
    }


    /// <summary>
    /// 
    /// </summary>
    /// <param name="uri"></param>
    /// <returns></returns>
    IEnumerator GetRequest(string uri)
    {
        using (UnityWebRequest webRequest = UnityWebRequest.Get(uri))
        {
            // Request and wait for the desired page.
            yield return webRequest.SendWebRequest();

            string[] pages = uri.Split('/');
            int page = pages.Length - 1;

            switch (webRequest.result)
            {
                case UnityWebRequest.Result.ConnectionError:
                case UnityWebRequest.Result.DataProcessingError:
                    Debug.LogError(pages[page] + ": Error: " + webRequest.error);
                    break;
                case UnityWebRequest.Result.ProtocolError:
                    Debug.LogError(pages[page] + ": HTTP Error: " + webRequest.error);
                    break;
                case UnityWebRequest.Result.Success:
                    Debug.Log(pages[page] + ":\nReceived: " + webRequest.downloadHandler.text);

                    GetTFJSModels(webRequest.downloadHandler.text);
                    UpdateTFJSModel();
                    break;
            }
        }
    }


    /// <summary>
    /// Initialize the GUI dropdown list
    /// </summary>
    private void InitializeDropdown()
    {
        // Create list of webcam device names
        List<string> webcamNames = new List<string>();
        foreach (WebCamDevice device in webcamDevices) webcamNames.Add(device.name);

        // Remove default dropdown options
        webcamDropdown.ClearOptions();
        // Add webcam device names to dropdown menu
        webcamDropdown.AddOptions(webcamNames);
        // Set the value for the dropdown to the current webcam device
        webcamDropdown.SetValueWithoutNotify(webcamNames.IndexOf(currentWebcam));

        string modelListPath = $"{Application.streamingAssetsPath}/models.json";
        StartCoroutine(GetRequest(modelListPath));

        // Remove default dropdown options
        backendDropdown.ClearOptions();
        // Add TFJS backend names to menu
        backendDropdown.AddOptions(tfjsBackends);
        // Select the first option in the dropdown
        backendDropdown.SetValueWithoutNotify(0);
    }


    /// <summary>
    /// Resize and position the main camera based on an in-scene screen object
    /// </summary>
    /// <param name="screenDims">The dimensions of an in-scene screen object</param>
    private void InitializeCamera(Vector2Int screenDims, string cameraName = "Main Camera")
    {
        // Get a reference to the Main Camera GameObject
        GameObject camera = GameObject.Find(cameraName);
        // Adjust the camera position to account for updates to the screenDims
        camera.transform.position = new Vector3(screenDims.x / 2, screenDims.y / 2, -10f);
        // Render objects with no perspective (i.e. 2D)
        camera.GetComponent<Camera>().orthographic = true;
        // Adjust the camera size to account for updates to the screenDims
        camera.GetComponent<Camera>().orthographicSize = screenDims.y / 2;
    }


    // Awake is called when the script instance is being loaded
    private void Awake()
    {
        WebGLPluginJS.GetExternalJS();
    }


    // Start is called before the first frame update
    void Start()
    {
        // Get the source image texture
        imageTexture = screen.gameObject.GetComponent<MeshRenderer>().material.mainTexture;
        // Get the source image dimensions as a Vector2Int
        imageDims = new Vector2Int(imageTexture.width, imageTexture.height);

        // Initialize list of available webcam devices
        webcamDevices = WebCamTexture.devices;
        foreach (WebCamDevice device in webcamDevices) Debug.Log(device.name);
        currentWebcam = webcamDevices[0].name;
        useWebcam = webcamDevices.Length > 0 ? useWebcam : false;
        // Initialize webcam
        if (useWebcam) InitializeWebcam(currentWebcam);

        // Resize and position the screen object using the source image dimensions
        InitializeScreen();
        // Resize and position the main camera using the source image dimensions
        InitializeCamera(screenDims);

        // Initialize list of class labels from JSON file
        classes = JsonUtility.FromJson<ClassLabels>(classLabels.text).classes;

        // Initialize the webcam dropdown list
        InitializeDropdown();

        WebGLPluginJS.SetTFJSBackend(tfjsBackends[backendDropdown.value]);
    }


    /// <summary>
    /// Scale the source image resolution to the target input dimensions
    /// while maintaing the source aspect ratio.
    /// </summary>
    /// <param name="imageDims"></param>
    /// <param name="targetDims"></param>
    /// <returns></returns>
    private Vector2Int CalculateInputDims(Vector2Int imageDims, int targetDim)
    {
        // Clamp the minimum dimension value to 64px
        targetDim = Mathf.Max(targetDim, 64);

        Vector2Int inputDims = new Vector2Int();

        // Calculate the input dimensions using the target minimum dimension
        if (imageDims.x >= imageDims.y)
        {
            inputDims[0] = (int)(imageDims.x / ((float)imageDims.y / (float)targetDim));
            inputDims[1] = targetDim;
        }
        else
        {
            inputDims[0] = targetDim;
            inputDims[1] = (int)(imageDims.y / ((float)imageDims.x / (float)targetDim));
        }

        return inputDims;
    }


    // Update is called once per frame
    void Update()
    {
        useWebcam = webcamDevices.Length > 0 ? useWebcam : false;
        if (useWebcam)
        {
            // Initialize webcam if it is not already playing
            if (!webcamTexture || !webcamTexture.isPlaying) InitializeWebcam(currentWebcam);

            // Skip the rest of the method if the webcam is not initialized
            if (webcamTexture.width <= 16) return;

            // Make sure screen dimensions match webcam resolution when using webcam
            if (screenDims.x != webcamTexture.width)
            {
                // Resize and position the screen object using the source image dimensions
                InitializeScreen();
                // Resize and position the main camera using the source image dimensions
                InitializeCamera(screenDims);
            }
        }
        else if (webcamTexture && webcamTexture.isPlaying)
        {
            // Stop the current webcam
            webcamTexture.Stop();

            // Resize and position the screen object using the source image dimensions
            InitializeScreen();
            // Resize and position the main camera using the source image dimensions
            InitializeCamera(screenDims);
        }

        // Scale the source image resolution
        Vector2Int inputDims = CalculateInputDims(screenDims, targetDim);

        // Initialize the input texture with the calculated input dimensions
        inputTextureGPU = RenderTexture.GetTemporary(inputDims.x, inputDims.y, 24, RenderTextureFormat.ARGB32);

        if (!inputTextureCPU || inputTextureCPU.width != inputTextureGPU.width)
        {
            inputTextureCPU = new Texture2D(inputDims.x, inputDims.y, TextureFormat.RGB24, false);
        }

        if (printDebugMessages) Debug.Log($"Input Dims: {inputTextureGPU.width}x{inputTextureGPU.height}");

        // Copy the source texture into model input texture
        Graphics.Blit((useWebcam ? webcamTexture : imageTexture), inputTextureGPU);

        // Download pixel data from GPU to CPU
        RenderTexture.active = inputTextureGPU;
        inputTextureCPU.ReadPixels(new Rect(0, 0, inputTextureGPU.width, inputTextureGPU.height), 0, 0);
        inputTextureCPU.Apply();

        int width = inputTextureCPU.width;
        int height = inputTextureCPU.height;
        int size = width * height * 3;
        isInitialized = WebGLPluginJS.PerformInference(inputTextureCPU.GetRawTextureData(), size, width, height);

        // Release the input texture
        RenderTexture.ReleaseTemporary(inputTextureGPU);

        // Check if index is valid
        if (printDebugMessages) Debug.Log(isInitialized ? $"Predicted Class: {classes[(int)output_data[0]]}" : "Not Initialized");

        // Release the input texture
        RenderTexture.ReleaseTemporary(inputTextureGPU);
    }


    /// <summary>
    /// This method is called when the value for the webcam toggle changes
    /// </summary>
    /// <param name="useWebcam"></param>
    public void UpdateWebcamToggle(bool useWebcam)
    {
        this.useWebcam = useWebcam;
    }


    /// <summary>
    /// 
    /// </summary>
    public void UpdateTFJSBackend()
    {
        WebGLPluginJS.SetTFJSBackend(tfjsBackends[backendDropdown.value]);
    }


    /// <summary>
    /// The method is called when the selected value for the webcam dropdown changes
    /// </summary>
    public void UpdateWebcamDevice()
    {
        currentWebcam = webcamDevices[webcamDropdown.value].name;
        Debug.Log($"Selected Webcam: {currentWebcam}");
        // Initialize webcam if it is not already playing
        if (useWebcam) InitializeWebcam(currentWebcam);

        // Resize and position the screen object using the source image dimensions
        InitializeScreen();
        // Resize and position the main camera using the source image dimensions
        InitializeCamera(screenDims);
    }


    /// <summary>
    /// 
    /// </summary>
    public void UpdateTFJSModel()
    {
        string modelName = modelNames[modelDropdown.value];

        foreach (TextAsset textAsset in normalizationStatsList)
        {
            if (modelName.Contains(textAsset.name.Split("-")[0]))
            {
                // Initialize the normalization stats from JSON file
                mean = JsonUtility.FromJson<NormalizationStats>(textAsset.text).mean;
                std = JsonUtility.FromJson<NormalizationStats>(textAsset.text).std;
            }
        }

        if (mean.Length == 0)
        {
            Debug.Log("Unable to find normalization stats");
            return;
        }
        {
            string mean_str = "";
            foreach (float val in mean) mean_str += $"{val} ";
            Debug.Log($"Mean Stats: {mean_str}");
            string std_str = "";
            foreach (float val in std) std_str += $"{val} ";
            Debug.Log($"Std Stats: {std_str}");
        }


        WebGLPluginJS.InitTFJSModel(modelPaths[modelDropdown.value], mean, std, output_data, output_data.Length);
    }


    // OnGUI is called for rendering and handling GUI events.
    public void OnGUI()
    {
        // Define styling information for GUI elements
        GUIStyle style = new GUIStyle
        {
            fontSize = (int)(Screen.width * (1f / (100f - fontScale)))
        };
        style.normal.textColor = textColor;

        // Define screen spaces for GUI elements
        Rect slot1 = new Rect(10, 10, 500, 500);
        Rect slot2 = new Rect(10, style.fontSize * 1.5f, 500, 500);

        // Verify predicted class index is valid
        string labelText = $"{classes[(int)output_data[0]]} {(output_data[1] * 100).ToString("0.##")}%";
        string content = isInitialized ? $"Predicted Class: {labelText}" : "Loading Model...";
        if (displayPredictedClass) GUI.Label(slot1, new GUIContent(content), style);

        // Update framerate value
        if (Time.unscaledTime > fpsTimer)
        {
            fps = (int)(1f / Time.unscaledDeltaTime);
            fpsTimer = Time.unscaledTime + fpsRefreshRate;
        }

        // Adjust screen position when not showing predicted class
        Rect fpsRect = displayPredictedClass ? slot2 : slot1;
        if (displayFPS) GUI.Label(fpsRect, new GUIContent($"FPS: {fps}"), style);
    }
}
