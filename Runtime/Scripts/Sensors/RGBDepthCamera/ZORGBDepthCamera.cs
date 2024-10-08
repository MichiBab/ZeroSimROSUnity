﻿using System.IO;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using AsyncGPUReadbackPluginNs;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using ZO.Util;

namespace ZO.Sensors
{

    /// <summary>
    /// A RGB image + depth camera sensor. 
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class ZORGBDepthCamera : ZOGameObjectBase
    {

        public enum FrameOutputType
        {
            RGB8D32,
            RGB8D16
        };

        [Header("Camera Parameters")]
        public Camera _camera;

        /// <summary>
        /// The Unity Camera associated with this depth camera. (readonly)
        /// </summary>
        /// <value></value>
        public Camera UnityCamera
        {
            get => _camera;
        }

        public int _width = 1280;

        /// <summary>
        /// Camera frame width in pixels.  (readonly)
        /// </summary>
        /// <value></value>
        public int Width
        {
            get => _width;
        }

        public int _height = 720;

        /// <summary>
        /// Camera frame height in pixels. (readonly)
        /// </summary>
        /// <value></value>
        public int Height
        {
            get => _height;
        }

        public float _undistort_coef_x = 0.67f;

        /// <summary>
        /// 
        /// </summary>
        /// <value></value>
        public float UndistortCoefX
        {
            get => _undistort_coef_x;
            set => _undistort_coef_x = value;
        }

        public float _undistort_coef_y = 0.67f;

        /// <summary>
        /// 
        /// </summary>
        /// <value></value>
        public float UndistortCoefY
        {
            get => _undistort_coef_y;
            set => _undistort_coef_y = value;
        }

        public float _sensor_size_x = 0.5f;

        /// <summary>
        /// 
        /// </summary>
        /// <value></value>
        public float SensorSizeX
        {
            get => _sensor_size_x;
            set => _sensor_size_x = value;
        }

        public float _sensor_size_y = 0.5f;

        /// <summary>
        /// 
        /// </summary>
        /// <value></value>
        public float SensorSizeY
        {
            get => _sensor_size_y;
            set => _sensor_size_y = value;
        }

        public float _current_focal_l_in_mm = 0.5f;

        /// <summary>
        /// 
        /// </summary>
        /// <value></value>
        public float CurrentFocalLInMM
        {
            get => _current_focal_l_in_mm;
            set => _current_focal_l_in_mm = value;
        }


        /// <summary>
        /// The cameras field of view in degrees.
        /// </summary>
        /// <value></value>
        float _fieldOfViewDegrees = 0;
        public float FieldOfViewDegrees
        {
            get => _fieldOfViewDegrees;
        }

        float _focalLengthMM = 0;
        /// <summary>
        /// The camera focal length in millimeters.
        /// </summary>
        /// <value></value>
        public float FocalLengthMM
        {
            get => _focalLengthMM;
        }

        Vector2 _sensorSizeMM = new Vector2();
        /// <summary>
        /// Sensor width and height in millimeters.
        /// </summary>
        /// <value></value>
        public Vector2 SensorSizeMM
        {
            get => _sensorSizeMM;
        }





        //public FrameOutputType _frameOutputType = FrameOutputType.RGB8D16;

        [Header("Depth")]
        public Material _rgbDepthCameraShader;
        public float _depthScale = 1.0f;



        [Header("Internal")]
        public int _maxAsyncGPURequestQueue = 2;
        public int _maxPublishTaskQueue = 2;


        // ~~~~~~ Delegate Callbacks ~~~~~~
        /// <summary>
        /// Called every frame passing in:
        /// this, string cameraId, width, height, RGB24[] image, float32[] depth
        /// 
        /// Note: is async so returns a task
        /// </summary>
        /// 
        /// <value></value>
        public Func<ZORGBDepthCamera, string, int, int, byte[], float[], Task> OnPublishDelegate { get; set; }

        // ~~~~~~ Render Buffers ~~~~~~~
        private RenderTexture _colorBuffer;
        private RenderTexture _depthBuffer;

        // the color texture used for debug rendering
        private Texture2D _colorTexture;

        // the depth texture used for debug rendering
        private Texture2D _depthTexture;

        // the final color and depth render texture which is of type ARGBFloat 
        // Alpha channel contains depth image.
        private RenderTexture _colorDepthRenderTexture;
        private bool _isOpenGLRenderer = false;

        // ~~~~ Async GPU Read ~~~~ //
        Queue<AsyncGPUReadbackPluginRequest> _asyncGPURequests = new Queue<AsyncGPUReadbackPluginRequest>();

        // the final native arrays that contain the F32 depth values and RGB8 color values
        private NativeArray<float> _depthBufferValuesFloat;
        private NativeArray<byte> _rgbValues;

        // publish queue
        Task _publishTask = null;



        protected override void ZOOnValidate()
        {
            base.ZOOnValidate();


            // if camera is not assigned see if we have a camera component on this game object
            if (_camera == null && TryGetComponent<Camera>(out _camera) == false)
            {
                _camera = gameObject.AddComponent<Camera>();
            }

            _camera.enabled = false;

            // if no post process material then assign default
            if (_rgbDepthCameraShader == null)
            {
                _rgbDepthCameraShader = Resources.Load<Material>("ZORGBDMaterial");
            }

            if (UpdateRateHz == 0)
            {
                UpdateRateHz = 10;
            }

            if (string.IsNullOrEmpty(Name) == true)
            {
                Name = gameObject.name + "_" + Type;
            }
        }


        protected override void ZOAwake()
        {
            base.ZOAwake();
            _fieldOfViewDegrees = UnityCamera.fieldOfView;
            if (UnityCamera.usePhysicalProperties)
            {
                _focalLengthMM = UnityCamera.focalLength;
                _sensorSizeMM = UnityCamera.sensorSize;
            }
        }

        // Start is called before the first frame update
        protected override void ZOStart()
        {
            if (_camera == null)
            {
                _camera = this.GetComponent<Camera>();
            }


            // setup the buffers
            _colorBuffer = new RenderTexture(_width, _height, 0, RenderTextureFormat.ARGB32);
            _depthBuffer = new RenderTexture(_width, _height, 32, RenderTextureFormat.Depth);
            _camera.SetTargetBuffers(_colorBuffer.colorBuffer, _depthBuffer.depthBuffer);

            // setup depth texture mode for the camera
            _camera.depthTextureMode = _camera.depthTextureMode | DepthTextureMode.Depth;

            _colorTexture = new Texture2D(_width, _height, TextureFormat.RGB24, false);
            _depthTexture = new Texture2D(_width, _height, TextureFormat.RGBAFloat, false);

            // create the render texture.  RGB is color and alpha is depth.  Float32
            _colorDepthRenderTexture = new RenderTexture(_width, _height, 0, RenderTextureFormat.ARGBFloat);

            // create the final publish buffers where the color buffer is converted from Float32 to RGB8
            _depthBufferValuesFloat = new NativeArray<float>(_width * _height, Allocator.Persistent);
            _rgbValues = new NativeArray<byte>(_width * _height * 3, Allocator.Persistent);

            _depthBufferFloat = new float[_width * _height];
            _colorPixels24 = new byte[_width * _height * 3];

            if (SystemInfo.supportsAsyncGPUReadback == true)
            {
                Debug.Log("INFO: Native support for AsyncGPUReadback");
            }
            else
            {
                Debug.Log("WARNING: NO support for native AsyncGPUReadback. Using 3rd party.");
            }

            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.OpenGLCore)
            {
                _isOpenGLRenderer = true;
            }

        }

        protected override void ZOFixedUpdateHzSynchronized()
        {
            _camera.Render();
        }

        protected override void ZOUpdate()
        {
            DoRenderTextureUpdate();
        }


        float _averageDepth = 0;
        protected override void ZOOnGUI()
        {
            base.ZOOnGUI();
            GUI.DrawTexture(new Rect(10, 10, 320, 240), _colorBuffer, ScaleMode.ScaleToFit);
            GUI.DrawTexture(new Rect(10, 242, 320, 240), _depthBuffer, ScaleMode.ScaleToFit);
            GUI.DrawTexture(new Rect(325, 242, 320, 240), _colorDepthRenderTexture, ScaleMode.ScaleToFit, true);

            GUI.Label(new Rect(320, 10, 100, 30), "Depth: " + _averageDepth.ToString());
        }



        private float[] _depthBufferFloat;
        private byte[] _colorPixels24;

        private void OnPostRender()
        {
            UnityEngine.Profiling.Profiler.BeginSample("ZORGBDepthCamera::OnPostRender");
            Rect cameraRect = new Rect(0, 0, _width, _height);
            _rgbDepthCameraShader.SetTexture("_MainTex", _colorBuffer);
            _rgbDepthCameraShader.SetTexture("_CameraDepthTexture", _depthBuffer);
            Graphics.Blit(_camera.targetTexture, _colorDepthRenderTexture, _rgbDepthCameraShader);

            if (_asyncGPURequests.Count < _maxAsyncGPURequestQueue)
            {
                _asyncGPURequests.Enqueue(AsyncGPUReadbackPlugin.Request(_colorDepthRenderTexture));
            }
            UnityEngine.Profiling.Profiler.EndSample();
        }

        private void DoRenderTextureUpdate()
        {
            // ~~~ Handle Async GPU Readback ~~~ //
            while (_asyncGPURequests.Count > 0)
            {
                var asyncGPURequest = _asyncGPURequests.Peek();
                // You need to explicitly ask for an update regularly
                asyncGPURequest.Update();

                if (asyncGPURequest.hasError)
                {
                    Debug.LogError("ERROR: GPU readback error detected.");
                    asyncGPURequest.Dispose();
                    _asyncGPURequests.Dequeue();
                }
                else if (asyncGPURequest.done)
                {
                    // Get data from the request when it's done
                    float[] rawTextureData = asyncGPURequest.GetRawData_ArrayFloat();
                    if (OnPublishDelegate != null)
                    {

                        if (_publishTask == null || _publishTask.IsCompleted)
                        {
                            if (!UnityCamera.usePhysicalProperties)
                            {
                                _focalLengthMM = _current_focal_l_in_mm;
                                _sensorSizeMM = new Vector2(_sensor_size_x, _sensor_size_y);
                            }

                            float focalLengthX = _focalLengthMM * _width / _sensorSizeMM.x;
                            float focalLengthY = _focalLengthMM * _height / _sensorSizeMM.y;
                            float principalPointX = _width / 2.0f;
                            float principalPointY = _height / 2.0f;
                            float r, g, b, d;
                            for (int z = 0, c = 0, p = 0; z < (_width * _height); z++, c += 3, p += 4)
                            {
                                r = rawTextureData[p];
                                g = rawTextureData[p + 1];
                                b = rawTextureData[p + 2];
                                d = rawTextureData[p + 3];

                                _colorPixels24[c + 0] = (byte)(r * 255.0f);
                                _colorPixels24[c + 1] = (byte)(g * 255.0f);
                                _colorPixels24[c + 2] = (byte)(b * 255.0f);

                                /*This currently only works with the Physical Camera:This currently only works with:
                                With Camera 640 x 480: 

                                22.16159 focal length
                                sensor size x: 54.12, y: 36.08.

                                or

                                With Camera 672 x 480: (1/4)
                                38.64568 focal length
                                seonsor size x: 54.12, y: 36.08


                                Changing the fov or the coefs results in
                                bad depth data again. But with them they are actually stationary and correct, measured
                                with the 2d lidar next to it.*/
                                float x = ((z % _width - principalPointX) / focalLengthX) * _undistort_coef_x;
                                float y = ((z / _width - principalPointY) / focalLengthY) * _undistort_coef_y;
                                float r2 = (x * x) + (y * y);
                                float f = 1 + _depthScale * r2;
                                _depthBufferFloat[z] = d / f;
                                _averageDepth = d;

                            }

                            OnPublishDelegate(this, Name, _width, _height, _colorPixels24, _depthBufferFloat);

                        }
                        else
                        {
                            Debug.Log("INFO: ZORGBDepthCamera publish task overflow...");
                        }

                    }

                    // You need to explicitly Dispose data after using them
                    asyncGPURequest.Dispose();

                    _asyncGPURequests.Dequeue();
                }
                else
                {
                    break;
                }
            }

        }
        private void OnDestroy()
        {
            _depthBufferValuesFloat.Dispose();
            _rgbValues.Dispose();
        }

        public string Type
        {
            get { return "sensor.rgbdebthgcamera"; }
        }

        [SerializeField] public string _name;
        public string Name
        {
            get
            {
                return _name;
            }
            private set
            {
                _name = value;
            }
        }


    }

}
