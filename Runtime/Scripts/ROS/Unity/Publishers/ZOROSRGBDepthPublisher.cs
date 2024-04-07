using System;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json.Linq;
using ZO.ROS.MessageTypes.Sensor;
using ZO.Sensors;
using ZO.ROS.Unity;
using ZO.ROS.MessageTypes.Geometry;

namespace ZO.ROS.Publisher
{

    /// <summary>
    /// ROS Publisher for `ZORGBDepthCamera`.  
    /// 
    /// Follows ROS `depth_image_proc` topics <see>http://wiki.ros.org/depth_image_proc</see>:
    /// 
    /// Publishes ROS Topics:
    ///     `rgb/camera_info (sensor_msgs/CameraInfo)`: Camera calibration and metadata. 
    ///     `rgb/image_rect_color (sensor_msgs/Image)`: Rectified color image. 
    ///     `depth_registered/image_rect (sensor_msgs/Image)`: Rectified depth image, registered to the RGB camera. 
    /// 
    /// </summary>
    public class ZOROSRGBDepthPublisher : ZOROSUnityGameObjectBase
    {

        [Header("RGB Depth Publisher")]
        public ZORGBDepthCamera _rgbDepthCameraSensor;

        [Header("ROS Topics")]
        public string _rgbImageROSTopic = "rgb/image_rect_color";
        public string _depthROSTopic = "depth_registered/image_rect";
        public string _cameraInfoROSTopic = "rgb/camera_info";
        public string _depthCameraInfoROSTopic = "depth_registered/camera_info";

        [Header("ROS Transforms")]
        public string _parentTransformName = "world";
        public string _depthTransformName = "depth_tf";
        public string _rgbTransformName = "rgb_tf";

        public Vector3 _cameraRotationDegrees = new Vector3(270, 270, 0);

        /// <summary>
        /// The depth camera sensor that we will publish.
        /// </summary>
        /// <value></value>
        public ZORGBDepthCamera RGBDepthCameraSensor
        {
            get => _rgbDepthCameraSensor;
            set => _rgbDepthCameraSensor = value;
        }
        private CompressedImageMessage _colorImageMessage = new CompressedImageMessage();
        private ImageMessage _depthImageMessage = new ImageMessage();
        private CameraInfoMessage _cameraInfoMessage = new CameraInfoMessage();
        private CameraInfoMessage _depthInfoMessage = new CameraInfoMessage();

        private TransformStampedMessage _depthTransformMessage = new TransformStampedMessage();
        private TransformStampedMessage _colorImageTransformMessage = new TransformStampedMessage();

        protected override void ZOOnValidate()
        {
            base.ZOOnValidate();
            if (UpdateRateHz == 0)
            {
                UpdateRateHz = 30;
            }

            if (RGBDepthCameraSensor == null)
            {
                TryGetComponent<ZORGBDepthCamera>(out _rgbDepthCameraSensor);
            }
        }


        protected override void ZOStart()
        {
            base.ZOStart();
            if (ZOROSBridgeConnection.Instance.IsConnected)
            {
                Initialize();
            }
            // initialize the camera info
            if (_rgbDepthCameraSensor.UnityCamera.usePhysicalProperties == true)
            {
                _cameraInfoMessage.BuildCameraInfo((uint)_rgbDepthCameraSensor.Width, (uint)_rgbDepthCameraSensor.Height,
                                                (double)_rgbDepthCameraSensor.FocalLengthMM,
                                                (double)_rgbDepthCameraSensor.SensorSizeMM.x, (double)_rgbDepthCameraSensor.SensorSizeMM.y);
                _depthInfoMessage.BuildCameraInfo((uint)_rgbDepthCameraSensor.Width, (uint)_rgbDepthCameraSensor.Height,
                                                (double)_rgbDepthCameraSensor.FocalLengthMM,
                                                (double)_rgbDepthCameraSensor.SensorSizeMM.x, (double)_rgbDepthCameraSensor.SensorSizeMM.y);

            }
            else
            {
                _cameraInfoMessage.BuildCameraInfo((uint)_rgbDepthCameraSensor.Width, (uint)_rgbDepthCameraSensor.Height, (double)_rgbDepthCameraSensor.FieldOfViewDegrees);
                _depthInfoMessage.BuildCameraInfo((uint)_rgbDepthCameraSensor.Width, (uint)_rgbDepthCameraSensor.Height, (double)_rgbDepthCameraSensor.FieldOfViewDegrees);

            }



        }

        protected override void ZOOnDestroy()
        {
            base.ZOOnDestroy();
            Terminate();
        }

        protected override void ZOUpdateHzSynchronized()
        {
            base.ZOUpdateHzSynchronized();

            string rootName = gameObject.transform.root.gameObject.name;


            // publish TF
            _depthTransformMessage.header.Update();
            _depthTransformMessage.header.frame_id = rootName + "_" + _parentTransformName;
            _depthTransformMessage.child_frame_id = rootName + "_" + _depthTransformName;
            // _depthTransformMessage.transform.translation.FromUnityVector3ToROS(Quaternion.Euler(_cameraRotationDegrees) * transform.localPosition);
            _depthTransformMessage.transform.translation.FromUnityVector3ToROS(transform.localPosition);

            _depthTransformMessage.transform.rotation.FromUnityQuaternionToROS(Quaternion.Euler(_cameraRotationDegrees) * transform.localRotation);

            // _depthTransformMessage.FromLocalUnityTransformToROS(this.transform * );

            ROSUnityManager.BroadcastTransform(_depthTransformMessage);

            // publish TF
            _colorImageTransformMessage.header.Update();
            _colorImageTransformMessage.header.frame_id = rootName + "_" + _parentTransformName;
            _colorImageTransformMessage.child_frame_id = rootName + "_" + _rgbTransformName;
            // _colorImageTransformMessage.FromLocalUnityTransformToROS(this.transform);
            // _colorImageTransformMessage.transform.translation.FromUnityVector3ToROS(Quaternion.Euler(_cameraRotationDegrees) * transform.localPosition);
            _colorImageTransformMessage.transform.translation.FromUnityVector3ToROS(transform.localPosition);

            _colorImageTransformMessage.transform.rotation.FromUnityQuaternionToROS(Quaternion.Euler(_cameraRotationDegrees) * transform.localRotation);


            ROSUnityManager.BroadcastTransform(_colorImageTransformMessage);

        }

        private void Initialize()
        {
            // advertise
            ROSBridgeConnection.Advertise(_rgbImageROSTopic, _colorImageMessage.MessageType);
            ROSBridgeConnection.Advertise(_depthROSTopic, _depthImageMessage.MessageType);
            ROSBridgeConnection.Advertise(_cameraInfoROSTopic, _cameraInfoMessage.MessageType);
            ROSBridgeConnection.Advertise(_depthCameraInfoROSTopic, _depthInfoMessage.MessageType);


            // setup the transforms
            _colorImageMessage.header.frame_id = _rgbTransformName;
            _depthImageMessage.header.frame_id = _depthTransformName;
            _cameraInfoMessage.header.frame_id = _rgbTransformName;
            _depthInfoMessage.header.frame_id = _depthTransformName;

            // hookup to the sensor update delegate
            _rgbDepthCameraSensor.OnPublishDelegate = OnPublishRGBDepthDelegate;

        }

        private void Terminate()
        {
            // unadvertise
            ROSBridgeConnection?.UnAdvertise(_rgbImageROSTopic);
            ROSBridgeConnection?.UnAdvertise(_depthROSTopic);
            ROSBridgeConnection?.UnAdvertise(_cameraInfoROSTopic);
            ROSBridgeConnection?.UnAdvertise(_depthCameraInfoROSTopic);

            // remove delegate
            _rgbDepthCameraSensor.OnPublishDelegate = null;

        }

        public override void OnROSBridgeConnected(ZOROSUnityManager rosUnityManager)
        {
            Debug.Log("INFO: ZOImagePublisher::OnROSBridgeConnected");
            Initialize();

        }

        public override void OnROSBridgeDisconnected(ZOROSUnityManager rosUnityManager)
        {
            Debug.Log("INFO: ZOImagePublisher::OnROSBridgeDisconnected");
            Terminate();
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

        private Task OnPublishRGBDepthDelegate(ZORGBDepthCamera rgbdCamera, string cameraId, int width, int height, byte[] rgbData, float[] depthData)
        {

            // publish rgb color image 
            _colorImageMessage.header.Update();

            Texture2D texture = new Texture2D(width, height);
            texture.SetPixels(ConvertBytesToColors(rgbData, width, height));
            texture.Apply();
            // Encode the Texture2D to JPEG format
            byte[] bytes = texture.EncodeToJPG(85);

            // Free the texture
            Destroy(texture);

            _colorImageMessage.data = bytes;
            _colorImageMessage.format = "jpeg";
            ROSBridgeConnection.Publish<CompressedImageMessage>(_colorImageMessage, _rgbImageROSTopic, cameraId);

            // publish depth image
            _depthImageMessage.header = _colorImageMessage.header; // color image & depth image header need to match
            _depthImageMessage.height = (uint)height;
            _depthImageMessage.width = (uint)width;
            _depthImageMessage.encoding = "32FC1";
            _depthImageMessage.is_bigendian = 0;
            _depthImageMessage.step = 4 * (uint)width;
            _depthImageMessage.data = new byte[4 * width * height];
            System.Buffer.BlockCopy(depthData, 0, _depthImageMessage.data, 0, 4 * width * height);
            ROSBridgeConnection.Publish<ImageMessage>(_depthImageMessage, _depthROSTopic, cameraId);

            _cameraInfoMessage.Update();
            _cameraInfoMessage.header = _colorImageMessage.header;
            _depthInfoMessage.Update();
            _depthInfoMessage.header = _depthImageMessage.header;
            ROSBridgeConnection.Publish<CameraInfoMessage>(_cameraInfoMessage, _cameraInfoROSTopic, cameraId);
            ROSBridgeConnection.Publish<CameraInfoMessage>(_depthInfoMessage, _depthCameraInfoROSTopic, cameraId);

            // publish TF


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
