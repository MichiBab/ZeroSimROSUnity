using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;
using ZO.ROS.MessageTypes.TF2;
using ZO.ROS.MessageTypes.Geometry;
using System.IO;
using System.Threading;
using ZO.ROS.MessageTypes.ROSGraph;
using ZO.ROS.MessageTypes.Std;
using ZO.ROS.MessageTypes.ZOSim;
using ZO.ROS.Publisher;
using ZO.Util;
using System.Collections.Concurrent;
namespace ZO.ROS.Unity
{
    /// <summary>
    /// Manage ROS with Unity specific functionality.
    /// </summary>
    [ExecuteAlways]
    public class ZOROSUnityManager : MonoBehaviour
    {


        /// <summary>
        /// The default ROS namespace for this simulation.
        /// </summary>
        public string _namespace = "/zerosim";



        public string Namespace
        {
            get => _namespace;
        }

        #region ROSBridgeConnection

        /// <summary>
        /// The singleton ROS Bridge Connection
        /// </summary>
        /// <value></value>
        public ZOROSBridgeConnection ROSBridgeConnection
        {
            get { return ZOROSBridgeConnection.Instance; }
        }


        /// <summary>
        /// IP or hostname of the ROS Bridge
        /// </summary>
        public string Hostname = "localhost";

        /// <summary>
        /// TCP Port for the ROS bridge. Do not change unless the default ROS Bridge port has been changed.
        /// </summary>
        public int Port = 9090;


        /// <summary>
        /// JSON or BSON serialization.  Recommended to stick with BSON as it is the most efficient.
        /// </summary>
        public ZOROSBridgeConnection.SerializationType _serializationType = ZOROSBridgeConnection.SerializationType.BSON;


        /// <summary>
        /// Event handler delegate definition that is used for ROS Bridge connect & disconnect events.
        /// </summary>
        /// <returns></returns>
        public delegate void ROSBridgeConnectionChangeHandler(ZOROSUnityManager sender);
        public event ROSBridgeConnectionChangeHandler _connectEvent;
        // public ZOROSBridgeConnectEvent _connectEvent = new ZOROSBridgeConnectEvent();

        /// <summary>
        /// Event that is called when connected to ROS bridge.
        /// </summary>
        /// <value></value>
        public event ROSBridgeConnectionChangeHandler ROSBridgeConnectEvent
        {
            add
            {
                _connectEvent += value;
            }
            remove
            {
                _connectEvent -= value;
            }
        }

        public event ROSBridgeConnectionChangeHandler _disconnectEvent;
        /// <summary>
        /// Event called when disconnected from ROS Bridge
        /// </summary>
        /// <returns></returns>
        public event ROSBridgeConnectionChangeHandler ROSBridgeDisconnectEvent
        {
            add
            {
                _disconnectEvent += value;
            }
            remove
            {
                _disconnectEvent -= value;
            }
        }
        #endregion // ROSBridgeConnection

        // #region ROS Docker Launch
        // public bool _launchROSDocker = false;

        // /// <summary>
        // /// If set then launch docker image with ROS and execute ROS launch file.  
        // /// </summary>
        // /// <value></value>
        // public bool LaunchROSDocker {
        //     get => _launchROSDocker;
        // }
        // public ZODockerRunParameters _rosLaunchParameters;

        // /// <summary>
        // /// The ROS Launch parameter scriptable object.
        // /// </summary>
        // /// <value></value>
        // public ZODockerRunParameters ROSLaunchParameters {
        //     get => _rosLaunchParameters;
        // }

        // #endregion // Docker Launch


        #region Tranform Publishing

        public string _worldFrameId = "map";
        /// <summary>
        /// The name of the world frame.  Usually "map" or "world".
        /// </summary>
        /// <value></value>
        public string WorldFrameId
        {
            get => _worldFrameId;
        }
        private TFMessage _transformBroadcast = new TFMessage();
        private List<TransformStampedMessage> _transformsToBroadcast = new List<TransformStampedMessage>();

        // publish 
        [SerializeField] public ZOROSTransformPublisher _rootMapTransformPublisher;
        public ZOROSTransformPublisher RootMapTransform
        {
            get => _rootMapTransformPublisher;
            private set
            {
                _rootMapTransformPublisher = value;
                _rootMapTransformPublisher.FrameID = "";
                _rootMapTransformPublisher.ChildFrameID = WorldFrameId;
                _rootMapTransformPublisher.UpdateRateHz = 1.0f;
                _rootMapTransformPublisher.ROSTopic = "";
            }
        }

        /// <summary>
        /// The ROS "/tf" topic broadcast. Provides an easy way to publish coordinate frame transform information. 
        /// </summary>
        /// <param name="transformStamped"></param> 
        public void BroadcastTransform(TransformStampedMessage transformStamped)
        {
            //Filter out bad transfroms
            if (transformStamped.header.frame_id == transformStamped.child_frame_id)
            {
                return;
            }
            if (ROSBridgeConnection.IsConnected)
            {
                _transformsToBroadcast.Add(transformStamped);
            }

        }

        #endregion // Transform Publishing


        #region Singleton
        private static ZOROSUnityManager _instance;

        /// <summary>
        /// Singleton access to this ROS Unity Manager.
        /// </summary>
        public static ZOROSUnityManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = (ZOROSUnityManager)FindObjectOfType<ZOROSUnityManager>();
                }
                return _instance;
            }
        }

        #endregion // Singleton



        #region Simulation Clock

        ClockMessage _clockMessage = new ClockMessage();

        /// <summary>
        /// sim
        /// </summary>
        /// <value></value>
        public ClockMessage Clock
        {
            get => _clockMessage;
        }
        #endregion // Simulation Clock


        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
                // DontDestroyOnLoad(this.gameObject);
            }
            else if (_instance != this)
            {
                Debug.LogError("ERROR: Cannot have two ZOROSUnityManager's!!!");
                Destroy(this.gameObject);
            }

        }

        private static string GetArg(string name)
        {
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == name && args.Length > i + 1)
                {
                    return args[i + 1];
                }
            }
            return null;
        }

        void ParseArguments()
        {
            //Change port from env variable.
            string port_string = GetArg("--port");
            if (port_string != null)
            {
                Port = Int32.Parse(port_string);
            }
            Debug.Log("Unity Ros Brdige Port set to : " + Port);

            // Array in form of: "NAME,X,Y,Rotation;..."
            string robot_starting_pose = GetArg("--robot_starting_poses");
            if (robot_starting_pose != null)
            {
                string[] poses = robot_starting_pose.Split(';');
                foreach (string pose in poses)
                {
                    string[] pose_parts = pose.Split(',');
                    if (pose_parts.Length == 4) 
                    {
                        string name = pose_parts[0];
                        float x = float.Parse(pose_parts[1]);
                        float y = float.Parse(pose_parts[2]);
                        float rotation = float.Parse(pose_parts[3]);
                        Debug.Log("Setting pose for " + name + " to " + x + "," + y + "," + rotation);
                        // Set the pose
                        GameObject go = GameObject.Find(name);
                        if (go != null)
                        {
                            float current_y = go.transform.position.y;
                            go.transform.position = new Vector3(x, current_y, y); // Since unity is Y up
                            go.transform.rotation = Quaternion.Euler(0, rotation, 0);
                        }
                    }
                }
            }
        }

        // Start is called before the first frame update
        void Start()
        {
            if (Application.IsPlaying(gameObject) == false)
            { // In Editor Mode 
                if (RootMapTransform == null)
                { // create the root map transform if doesn't exist
                    RootMapTransform = gameObject.AddComponent<ZOROSTransformPublisher>();
                    RootMapTransform.UpdateRateHz = 10.0f;
                }

                // 
                // if (_launchROSDocker == true) {
                //     string launchCommand = $"";
                //     // ZO.Editor.ZODockerManager.DockerRun("zosim", launchCommand);
                // }
            }
            else
            { // in play mode


                ParseArguments();

                ROSBridgeConnection.Serialization = _serializationType;
                ROSBridgeConnection.ROSBridgeConnectEvent += delegate (ZOROSBridgeConnection rosBridge)
                {
                    Debug.Log("INFO: Connected to ROS Bridge");

                    // advertise the transform broadcast
                    rosBridge.Advertise("/tf", _transformBroadcast.MessageType);

                    // advertise the simulation clock
                    rosBridge.Advertise("/clock", Clock.MessageType);

                    try
                    {
                        // inform listeners we have connected
                        _connectEvent.Invoke(this);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError("ERROR: ZOROSUnityManager Connected Invoke: " + e.ToString());
                    }
                };

                ROSBridgeConnection.ROSBridgeDisconnectEvent += delegate (ZOROSBridgeConnection rosBridge)
                {
                    Debug.Log("INFO: Disconnected to ROS Bridge");

                    // inform listeners we have disconnected
                    _disconnectEvent?.Invoke(this);

                    // Unadvertise broadcast tf 
                    rosBridge.UnAdvertise("/tf");

                    // Unadvertise simulation clock
                    rosBridge.UnAdvertise("/clock");
                };

                // run async task.  if cannot connect wait for a couple of seconds and try again
                ROSBridgeConnection.Port = Port;
                ROSBridgeConnection.Hostname = Hostname;
                Task rosBridgeConnectionTask = Task.Run(async () =>
                {
                    await ROSBridgeConnection.ConnectAsync();
                });

            }

        }


        private void OnDestroy()
        {
            ROSBridgeConnection.UnAdvertise("/tf");
            // ROSBridgeConnection.UnAdvertiseService(_namespace + "/spawn_zosim_model");
            ROSBridgeConnection.Stop();
        }

        private static long MIN_TF_TIME_IN_MS = 2; //equals 500 hz
        private static long last_tf_timestamp = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

        private static long MIN_CLOCK_TIME_IN_MS = 10; //equals 100 hz
        private static long last_clock_timestamp = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;

        // Update is called once per frame
        void Update()
        {
            // publish map transform
            if (ROSBridgeConnection.IsConnected)
            {
                //check if paused
                bool paused_state = Time.timeScale <= 0;
                var time_now = (DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond);
                // transform broadcast
                _transformBroadcast.transforms = _transformsToBroadcast.ToArray();
                if (_transformBroadcast.transforms.Length > 0)
                {
                    //only if the sim is not paused
                    if (time_now - last_tf_timestamp > MIN_TF_TIME_IN_MS && !paused_state)
                    {
                        last_tf_timestamp = time_now;
                        ROSBridgeConnection.Publish<TFMessage>(_transformBroadcast, "/tf");
                    }
                    _transformsToBroadcast.Clear();
                }
                // simulation clock
                Clock.Update();
                if (time_now - last_clock_timestamp > MIN_CLOCK_TIME_IN_MS && !paused_state)
                {
                    last_clock_timestamp = time_now;
                    ROSBridgeConnection.Publish<ClockMessage>(Clock, "/clock");
                }
            }
        }



    }
}
