using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Windows.WebCam;
using YoloV4Tiny;

public class MainController : MonoBehaviour
{
    public Transform ballParent;
    public GameObject ball;
    public GameObject quad;
    private Texture2D targetTexture;

    HoloLensCameraStream.Resolution resolution;
    private PhotoCapture photoCaptureObject = null;
    private Matrix4x4 cameraToWorldMatrix;
    private Matrix4x4 projectionMatrix;
    private Vector3 holoCamPos;
    private Quaternion holoCamRotation;
    private Quaternion faceHoloCamRotation;

    public float _threshold = 0.5f;
    public ResourceSet _resources = null;
    ObjectDetector _detector;

    public static string[] _labels = new[]
    {
        "Plane", "Bicycle", "Bird", "Boat",
        "Bottle", "Bus", "Car", "Cat",
        "Chair", "Cow", "Table", "Dog",
        "Horse", "Motorbike", "Person", "Plant",
        "Sheep", "Sofa", "Train", "TV"
    };

    #region Yolo Detect Obj

    void Start()
    {
        _detector = new ObjectDetector(_resources);
    }

    void OnDestroy()
    {
        _detector.Dispose();
    }

    void DetectObjs()
    {
        _detector.ProcessImage(targetTexture, _threshold);

        foreach (var d in _detector.Detections)
        {
            // Bounding box position
            var x = d.x * resolution.width;
            var y = d.y * resolution.height;
            /*var w = d.w * resolution.width;
            var h = d.h * resolution.height;*/

            // Label (class name + score)
            var name = _labels[(int)d.classIndex];
            string msg = $"{name} {(int)(d.score * 100)}%";

            Vector3 obj_center_pos = LocatableCameraUtils.PixelCoordToWorldCoord(cameraToWorldMatrix, projectionMatrix, resolution, new Vector2(x, y));
            RaycastHit hitInfo;
            if (Physics.Raycast(holoCamPos, obj_center_pos, out hitInfo, Mathf.Infinity, (1 << 31)))
            {
                CreateBall(hitInfo.point, msg);
            }
        }

    }

    void CreateBall(Vector3 pos, string msg)
    {
        GameObject obj = Instantiate(ball, ballParent);
        obj.transform.position = pos;
        obj.transform.localScale = new Vector3(0.02f, 0.02f, 0.02f);
        obj.transform.rotation = Quaternion.identity;
        obj.GetComponentInChildren<TextMeshPro>().text = msg;
        obj.SetActive(true);
    }

    #endregion

    #region PhotoCapture
    public void StartCapture()
    {
        Invoke("PhotoCaptureAndDetect", 1.5f);
    }

    void PhotoCaptureAndDetect()
    {
        PhotoCapture.CreateAsync(false, OnPhotoCaptureCreated);
    }

    void OnPhotoCaptureCreated(PhotoCapture captureObject)
    {
        photoCaptureObject = captureObject;

        Resolution cameraResolution = PhotoCapture.SupportedResolutions.OrderByDescending((res) => res.width * res.height).First();

        CameraParameters c = new CameraParameters();
        c.hologramOpacity = 0.0f; //change it more than 0 if you want to capture hologram
        c.cameraResolutionWidth = cameraResolution.width;
        c.cameraResolutionHeight = cameraResolution.height;
        c.pixelFormat = CapturePixelFormat.BGRA32;

        captureObject.StartPhotoModeAsync(c, OnPhotoModeStarted);
    }

    private void OnPhotoModeStarted(PhotoCapture.PhotoCaptureResult result)
    {
        if (result.success)
        {
            photoCaptureObject.TakePhotoAsync(OnCapturedPhotoToMemory);
        }
        else
        {
            Debug.LogError("Unable to start photo mode!");
        }
    }

    void OnCapturedPhotoToMemory(PhotoCapture.PhotoCaptureResult result, PhotoCaptureFrame photoCaptureFrame)
    {
        if (result.success)
        {
            // Create our Texture2D for use and set the correct resolution
            Resolution cameraResolution = PhotoCapture.SupportedResolutions.OrderByDescending((res) => res.width * res.height).First();
            targetTexture = new Texture2D(cameraResolution.width, cameraResolution.height);
            // Copy the raw image data into our target texture
            photoCaptureFrame.UploadImageDataToTexture(targetTexture);
            // Do as we wish with the texture such as apply it to a material, etc.

            resolution = new HoloLensCameraStream.Resolution(3904, 2196);// (targetTexture.width, targetTexture.height);
            if (TryGetHoloCamPosAndRotation(photoCaptureFrame))
                ShowCapturedTexture(targetTexture);
        }
        // Clean up
        photoCaptureObject.StopPhotoModeAsync(OnStoppedPhotoMode);
    }

    void OnStoppedPhotoMode(PhotoCapture.PhotoCaptureResult result)
    {
        photoCaptureObject.Dispose();
        photoCaptureObject = null;
        DetectObjs();
    }

    #endregion

    bool TryGetHoloCamPosAndRotation(PhotoCaptureFrame photoCaptureFrame)
    {
        bool isGetCameraToWorldMatrix = photoCaptureFrame.TryGetCameraToWorldMatrix(out cameraToWorldMatrix);
        bool isGetProjectionMatrix = photoCaptureFrame.TryGetProjectionMatrix(out projectionMatrix);

        if (isGetCameraToWorldMatrix)
        {
            holoCamPos = new Vector3(cameraToWorldMatrix[12], cameraToWorldMatrix[13], cameraToWorldMatrix[14]);

            holoCamRotation = cameraToWorldMatrix.rotation;

            Vector3 inverseNormal = -cameraToWorldMatrix.GetColumn(2);
            faceHoloCamRotation = Quaternion.LookRotation(inverseNormal, cameraToWorldMatrix.GetColumn(1));
        }

        return isGetCameraToWorldMatrix && isGetProjectionMatrix;
    }

    //shows what the captured photo look like in Hololens' PV Cam position
    void ShowCapturedTexture(Texture2D tex)
    {
        /*quad.GetComponent<MeshRenderer>().material.mainTexture = tex;

        Vector3 imageCenterDirection = LocatableCameraUtils.PixelCoordToWorldCoord(cameraToWorldMatrix, projectionMatrix, resolution, new Vector2(resolution.width / 2, resolution.height / 2));
        Vector3 imageTopLeftDirection = LocatableCameraUtils.PixelCoordToWorldCoord(cameraToWorldMatrix, projectionMatrix, resolution, new Vector2(0, 0));
        Vector3 imageTopRightDirection = LocatableCameraUtils.PixelCoordToWorldCoord(cameraToWorldMatrix, projectionMatrix, resolution, new Vector2(resolution.width, 0));
        Vector3 imageBotLeftDirection = LocatableCameraUtils.PixelCoordToWorldCoord(cameraToWorldMatrix, projectionMatrix, resolution, new Vector2(0, resolution.height));
        Vector3 imageBotRightDirection = LocatableCameraUtils.PixelCoordToWorldCoord(cameraToWorldMatrix, projectionMatrix, resolution, new Vector2(resolution.width, resolution.height));

        float x = Vector3.Distance(imageTopLeftDirection, imageTopRightDirection);
        float y = Vector3.Distance(imageTopLeftDirection, imageBotLeftDirection);
        quad.transform.localScale = new Vector3(x, y, 1);

        quad.transform.position = new Vector3(0, 0.6f, 0.6f); // imageCenterDirection + holoCamPos;

        quad.transform.rotation = faceHoloCamRotation;
        quad.gameObject.SetActive(true);*/
    }

}
