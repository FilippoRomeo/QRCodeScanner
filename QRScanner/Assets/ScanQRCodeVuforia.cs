using System;
using UnityEngine;
using UnityEngine.UI;

using Vuforia;
using static Vuforia.Image;
using ZXing;
using static ZXing.RGBLuminanceSource;

public class ScanQRCodeVuforia : MonoBehaviour
{
    #region Variables

    [SerializeField, Tooltip("Text UI Element to show seconds passed.")]
    /// <summary>
    /// Text UI Element to show seconds passed.
    /// </summary>
    private Text secondsPassedText = null;

    [SerializeField, Tooltip("Text UI Element to show the decoded text.")]
    /// <summary>
    /// Text UI Element to show the decoded text.
    /// </summary>
    private Text scannedText = null;

    [SerializeField, Tooltip("The RawImage to display the camera feed in.")]
    /// <summary>
    /// The RawImage to display the camera feed in.
    /// </summary>
    private RawImage cameraFeed = null;

    [SerializeField, Tooltip("Pixel Format supplied to Vuforia's Camera API.")]
    /// <summary>
    /// Image Frame Format supplied to Vuforia's Camera API.
    /// </summary>
    private PIXEL_FORMAT pixelFormat = PIXEL_FORMAT.RGBA8888;

    /// <summary>
    /// Barcode reader instance used for decoding QR codes.
    /// </summary>
    private IBarcodeReader barcodeReader;

    /// <summary>
    /// Time elapsed in seconds, displayed on screen.
    /// </summary>
    private double secondsPassed = 0f;

    /// <summary>
    /// Set whenever the application is active.
    /// </summary>
    private bool frameFormatRegistered = false;

    #endregion


    #region MonoBehaviour Callbacks

    /// <summary>
    /// Called before any MonoBehaviour Start callbacks.
    /// </summary>
    private void Awake()
    {
        // Disable logging when in build
#if UNITY_EDITOR
         Debug.unityLogger.logEnabled = true;
#else
        Debug.unityLogger.logEnabled = false;
#endif

        barcodeReader = new BarcodeReader();
        VuforiaARController.Instance.RegisterVuforiaStartedCallback(OnVuforiaStart);
        VuforiaARController.Instance.RegisterTrackablesUpdatedCallback(OnTrackablesUpdate);
        VuforiaARController.Instance.RegisterBackgroundTextureChangedCallback(OnBackgroundTextureChange);
        VuforiaARController.Instance.RegisterOnPauseCallback(OnPause);
    }

    /// <summary>
    /// Run when this MonoBehaviour is destroyed. Useful for unregistering callbacks or disposing native handles. 
    /// </summary>
    private void OnDestroy()
    {
        VuforiaARController.Instance.UnregisterVuforiaStartedCallback(OnVuforiaStart);
        VuforiaARController.Instance.UnregisterTrackablesUpdatedCallback(OnTrackablesUpdate);
        VuforiaARController.Instance.UnregisterBackgroundTextureChangedCallback(OnBackgroundTextureChange);
        VuforiaARController.Instance.UnregisterOnPauseCallback(OnPause);
    }

    /// <summary>
    /// MonoBehaviour callback for processing GUI in Unity
    /// </summary>
    private void OnGUI()
    {
        // show how much time passed
        secondsPassed += Time.deltaTime;
        secondsPassedText.text = $"{secondsPassed,-1:F2}s";
    }

    #endregion


    #region Vuforia Callbacks

    /// <summary>
    /// Run when Vuforia is initialised.
    /// </summary>
    private void OnVuforiaStart()
    {
        // Run the registration for the first time
        RegisterFrameFormat();
    }

    /// <summary>
    /// Run when changes in trackable objects are detected.
    /// </summary>
    private void OnTrackablesUpdate()
    {
        // only attempt to capture camera feed frames if pixel format was recognised
        if (frameFormatRegistered)
        {
            var cameraFeedFrame = CameraDevice.Instance.GetCameraImage(pixelFormat);
            if (cameraFeedFrame != null)
            {
                Debug.Log($@"IMAGE DETAILS
                 Format:      {cameraFeedFrame.PixelFormat}
                 Size:        {cameraFeedFrame.Width}x{cameraFeedFrame.Height}
                 Buffer Size: {cameraFeedFrame.BufferWidth}x{cameraFeedFrame.BufferHeight}
                 Stride:      {cameraFeedFrame.Stride}");

                ReadQRCode(cameraFeedFrame);
            }
        }
    }

    /// <summary>
    /// Run when the background texture changes. Useful for reading camera feeds.
    /// </summary>
    private void OnBackgroundTextureChange()
    {
        var videoTextureInfo = VuforiaRenderer.Instance.GetVideoTextureInfo();
        Debug.Log($@"OnBackgroundTextureChange() called with:
                   Image Size:   {videoTextureInfo.imageSize.x}x{videoTextureInfo.imageSize.y}
                   Texture Size: {videoTextureInfo.textureSize.x}x{videoTextureInfo.textureSize.y}");

        // If the video background texture is changed, reassign to the camera feed texture
        cameraFeed.texture = VuforiaRenderer.Instance.VideoBackgroundTexture;
    
    }

    /// <summary>
    /// Run when the application is paused from the system.
    /// </summary>
    /// <param name="paused">Whether the application has been paused or not. Supplied by the system.</param>
    private void OnPause(bool paused)
    {
        if (paused)
        {
            Debug.Log("App was paused.");
            UnregisterFrameFormat();
        }
        else
        {
            Debug.Log("App was resumed.");
            RegisterFrameFormat();
        }
    }

    /// <summary>
    /// Register the pixel format to be used for camera feed frames.
    /// </summary>
    private void RegisterFrameFormat()
    {
        if (frameFormatRegistered = CameraDevice.Instance.SetFrameFormat(pixelFormat, true))
        {
            Debug.Log($"Registered pixel format {pixelFormat}.");
        }
        else
        {
            Debug.LogError($@"Pixel format {pixelFormat} could not be registered.
             The format may not be supported by your device.
             Consider using a different pixel format.");
        }
    }

    /// <summary>
    /// Unregister the assigned pixel format.
    /// </summary>
    private void UnregisterFrameFormat()
    {
        Debug.Log($"Unregistering camera pixel format {pixelFormat}.");
        CameraDevice.Instance.SetFrameFormat(pixelFormat, false);
        // Ensure that the feed is not captured if the application is paused
        frameFormatRegistered = false;
    }

    #endregion


    #region Helper Methods

    /// <summary>
    /// Decode any single QR code present in a camera frame.
    /// </summary>
    /// <param name="image">The camera feed frame.</param>
    private void ReadQRCode(Vuforia.Image image)
    {
        if (image == null || image.Pixels == null)
        {
            return;
        }

        try
        {
            // decode the current frame
            var result = barcodeReader.Decode(image.Pixels, image.BufferWidth, image.BufferHeight, VuforiaToZXingBitmapFormat(pixelFormat));
            if (result != null)
            {
                scannedText.text = result.Text;
                Debug.Log($"Decoded {result.Text}");
            }
            else
            {
                Debug.Log("Nothing decoded yet.");
            }
        }
        catch (Exception e)
        {
            Debug.LogError(e.Message);
        }
    }

    /// <summary>
    /// Map Vuforia's PIXEL_FORMAT enumeration to ZXing's BitmapFormat enumeration wherever applicable.
    /// </summary>
    /// <param name="vuforiaFormat">The pixel format set in the inspector.</param>
    /// <returns>The corresponding BitmapFormat value.</returns>
    private static BitmapFormat VuforiaToZXingBitmapFormat(PIXEL_FORMAT vuforiaFormat)
    {
        switch (vuforiaFormat)
        {
            case PIXEL_FORMAT.GRAYSCALE:
                return BitmapFormat.Gray8;
            case PIXEL_FORMAT.RGB888:
                return BitmapFormat.RGB24;
            case PIXEL_FORMAT.RGBA8888:
                return BitmapFormat.RGBA32;
            case PIXEL_FORMAT.RGB565:
                return BitmapFormat.RGB565;
            default:
                Debug.LogError($"Vuforia {vuforiaFormat} does not have a corresponding ZXing BitmapFormat type. Please use another format type.");
                return BitmapFormat.Unknown;
        }
    }

    #endregion
}