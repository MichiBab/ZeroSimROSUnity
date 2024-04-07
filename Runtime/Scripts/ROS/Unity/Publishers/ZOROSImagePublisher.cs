using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json.Linq;
using ZO.ROS.MessageTypes.Sensor;
using ZO.Sensors;
using ZO.ROS.Unity;
using ZO.Document;
using System.IO;


namespace ZO.ROS.Publisher
{

    /// <summary>
    /// Publish /sensor/Image message.
    /// See: http://docs.ros.org/en/melodic/api/sensor_msgs/html/msg/Image.html
    /// To test run: `rosrun image_view image_view image:=/unity_image/image _image_transport:=raw`
    /// </summary>
    public class ZOROSImagePublisher : ZOROSUnityGameObjectBase
    {

        public ZORGBCamera _rgbCameraSensor;

        [Header("ROS Topics")]
        /// <summary>
        /// The ROS Image message topic to publish to.
        /// See: http://docs.ros.org/en/melodic/api/sensor_msgs/html/msg/Image.html
        /// </summary>
        public string _imageROSTopic = "image/image_raw/compressed";

        /// <summary>
        /// The CameraInfo message topic to publish to.
        /// See: http://docs.ros.org/en/melodic/api/sensor_msgs/html/msg/CameraInfo.html
        /// </summary>
        public string _cameraInfoROSTopic = "image/camera_info";


        /// <summary>
        /// The camera sensor that we will publish.
        /// </summary>
        /// <value></value>
        public ZORGBCamera RGBCameraSensor
        {
            get => _rgbCameraSensor;
            set => _rgbCameraSensor = value;
        }

        private CompressedImageMessage _rosImageMessage = new CompressedImageMessage();
        private CameraInfoMessage _rosCameraInfoMessage = new CameraInfoMessage();

        /// <summary>
        /// The ROS Image message topic to publish to.
        /// See: http://docs.ros.org/en/melodic/api/sensor_msgs/html/msg/Image.html
        /// </summary>
        /// <value></value>
        public string ImageROSTopic
        {
            get => _imageROSTopic;
            set => _imageROSTopic = value;
        }

        /// <summary>
        /// The CameraInfo message topic to publish to.
        /// See: http://docs.ros.org/en/melodic/api/sensor_msgs/html/msg/CameraInfo.html
        /// </summary>
        /// <value></value>
        public string CameraInfoROSTopic
        {
            get => _cameraInfoROSTopic;
            set => _cameraInfoROSTopic = value;
        }


        protected override void ZOStart()
        {
            base.ZOStart();
            if (ZOROSBridgeConnection.Instance.IsConnected)
            {
                Initialize();
            }
        }
        protected override void ZOOnDestroy()
        {
            ROSBridgeConnection?.UnAdvertise(CameraInfoROSTopic);
            ROSBridgeConnection?.UnAdvertise(ImageROSTopic);
            _rgbCameraSensor.OnPublishRGBImageDelegate = null;
        }

        protected override void ZOOnValidate()
        {
            base.ZOOnValidate();
            if (UpdateRateHz == 0)
            {
                UpdateRateHz = 30;
            }
            if (RGBCameraSensor == null)
            {
                RGBCameraSensor = GetComponent<ZORGBCamera>();
            }

            _imageROSTopic = "/" + gameObject.transform.root.gameObject.name + "/camera/face_cam/image_raw/compressed";
            _cameraInfoROSTopic = "/" + gameObject.transform.root.gameObject.name + "/camera/face_cam/camera_info";

        }

        private void Initialize()
        {
            // advertise
            ROSBridgeConnection.Advertise(ImageROSTopic, _rosImageMessage.MessageType);
            ROSBridgeConnection.Advertise(CameraInfoROSTopic, _rosCameraInfoMessage.MessageType);


            // hookup to the sensor update delegate
            // if (RGBCameraSensor.IsMonochrome == true) {
            //     _rgbCameraSensor.OnPublishRGBImageDelegate = OnPublishMonoImageDelegate;
            // } else {
            _rgbCameraSensor.OnPublishRGBImageDelegate = OnPublishRGBImageDelegate;
            // }


        }

        public override void OnROSBridgeConnected(ZOROSUnityManager rosUnityManager)
        {
            Debug.Log("INFO: ZOImagePublisher::OnROSBridgeConnected");
            Initialize();

        }

        public override void OnROSBridgeDisconnected(ZOROSUnityManager rosUnityManager)
        {
            Debug.Log("INFO: ZOImagePublisher::OnROSBridgeDisconnected");
            ROSBridgeConnection?.UnAdvertise(ImageROSTopic);
            ROSBridgeConnection?.UnAdvertise(CameraInfoROSTopic);
        }


        Color[] ConvertBytesToColors(byte[] bytes, int width, int height)
        {
            Color[] colors = new Color[width * height];
            int colorIndex = 0;
            for (int y = height - 1; y >= 0; y--)
            {
                for (int x = 0; x < width * 3; x += 3)
                {
                    float r = bytes[y * width * 3 + x] / 255f;
                    float g = bytes[y * width * 3 + x + 1] / 255f;
                    float b = bytes[y * width * 3 + x + 2] / 255f;
                    colors[colorIndex] = new Color(r, g, b);
                    colorIndex++;
                }
            }
            return colors;

        }



        /// <summary>
        /// Publishes raw camera RBG8 data as a ROS Image message.
        /// See: http://docs.ros.org/en/melodic/api/sensor_msgs/html/msg/Image.html
        /// </summary>
        /// <param name="rgbCamera">The camera component</param>
        /// <param name="cameraId">Camera ID</param>
        /// <param name="width">Frame width</param>
        /// <param name="height">Frame height</param>
        /// <param name="rgbData">Raw RBG8 data </param>
        /// <returns></returns>
        private Task OnPublishRGBImageDelegate(ZORGBCamera rgbCamera, string cameraId, int width, int height, byte[] rgbData)
        {

            // figure out the transforms
            ZOROSTransformPublisher transformPublisher = GetComponent<ZOROSTransformPublisher>();
            if (transformPublisher != null)
            {
                _rosImageMessage.header.frame_id = transformPublisher.ChildFrameID;
                _rosCameraInfoMessage.header.frame_id = transformPublisher.ChildFrameID;
            }
            else
            {
                _rosImageMessage.header.frame_id = gameObject.transform.root.gameObject.name + "_" + Name;
                _rosCameraInfoMessage.header.frame_id = gameObject.transform.root.gameObject.name + "_" + Name;
            }

            // setup and send Image message
            _rosImageMessage.header.Update();
            // Compress the image data using JPEG compression
            Texture2D texture = new Texture2D(width, height);
            texture.SetPixels(ConvertBytesToColors(rgbData, width, height));
            texture.Apply();
            // Encode the Texture2D to JPEG format
            byte[] bytes = texture.EncodeToJPG(85);

            //Free the texture
            Destroy(texture);

            _rosImageMessage.data = bytes;
            _rosImageMessage.format = "jpeg";
            ROSBridgeConnection.Publish<CompressedImageMessage>(_rosImageMessage, _imageROSTopic, Name);


            // setup and send CameraInfo message            
            _rosCameraInfoMessage.header = _rosImageMessage.header;
            // initialize the camera info
            if (RGBCameraSensor.UnityCamera.usePhysicalProperties == true)
            {
                _rosCameraInfoMessage.BuildCameraInfo((uint)RGBCameraSensor.Width, (uint)RGBCameraSensor.Height,
                                                (double)RGBCameraSensor.FocalLengthMM,
                                                (double)RGBCameraSensor.SensorSizeMM.x, (double)RGBCameraSensor.SensorSizeMM.y);
            }
            else
            {
                _rosCameraInfoMessage.BuildCameraInfo((uint)RGBCameraSensor.Width, (uint)RGBCameraSensor.Height, (double)RGBCameraSensor.FieldOfViewDegrees);
            }
            ROSBridgeConnection.Publish<CameraInfoMessage>(_rosCameraInfoMessage, _cameraInfoROSTopic, cameraId);

            return Task.CompletedTask;
        }

        #region ZOSerializationInterface
        public override string Type
        {
            get { return "ros.publisher.image"; }
        }


        #endregion // ZOSerializationInterface
    }

}
