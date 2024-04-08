using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;
using ZO.ROS.MessageTypes.Geometry;
using ZO.ROS.Unity;
using ZO.Document;

namespace ZO.ROS.Publisher
{
    /// <summary>
    /// Publish ROS TF of the Unity Transform this script is attached to.
    /// See: http://wiki.ros.org/tf
    /// </summary>
    public class ZOROSTransformPublisher : ZOROSUnityGameObjectBase
    {
        public string _frameId = "";
        public string _childFrameId = "";

        /// <summary>
        /// The name of *THIS* frame.
        /// </summary>
        /// <value></value>
        public string ChildFrameID
        {
            get => _childFrameId;
            set => _childFrameId = value;
        }

        /// <summary>
        /// The name of the parent frame.
        /// </summary>
        /// <value></value>
        public string FrameID
        {
            get => _frameId;
            set => _frameId = value;
        }

        private TransformStampedMessage _transformMessage = new TransformStampedMessage();

        protected override void ZOStart()
        {
            base.ZOStart();
            // if the child frame id is not set then set it to be the name of this game object.
            if (string.IsNullOrEmpty(ChildFrameID) == true)
            {
                ChildFrameID = this.gameObject.name;
            }

        }
        protected override void ZOUpdateHzSynchronized()
        {
            string rootName = gameObject.transform.root.gameObject.name;

            _transformMessage.header.Update();
            _transformMessage.header.frame_id = rootName + "_" + FrameID;
            _transformMessage.child_frame_id = rootName + "_" + ChildFrameID;
            _transformMessage.FromLocalUnityTransformToROS(this.transform);
            /*Sadly we need a workaround for things that are just inherintly different between the sim and the real loomo, e.g. the coordinate system...
                If someone is better at this than me, fix it in the loomo prefab while keeping the loomo in the sim still usable.
            */
            //Set wheel position a little more up, left_wheel_frame or right_wheel_frame
            if (ChildFrameID == "left_wheel_frame" || ChildFrameID == "right_wheel_frame")
            {
                _transformMessage.transform.translation.z += 0.16f;
            }
            //Set base_center_ground_frame a little more up
            if (ChildFrameID == "base_center_ground_frame")
            {
                _transformMessage.transform.translation.z += 0.03f;
            }

            if (ChildFrameID == "face_cam")
            {
                //Rotate y and z by 90 degrees
                Quaternion rotationY = Quaternion.Euler(0, 180, 0);
                // Quaternion representing rotation of 90 degrees over the z-axis
                Quaternion rotationZ = Quaternion.Euler(0, 0, 90);
                Quaternion originalQuaternion = Quaternion.Euler(0, 0, 0);
                // Rotate the original quaternion by 90 degrees over y-axis
                Quaternion rotatedQuaternionY = rotationY * originalQuaternion;
                // Rotate the quaternion rotated by y-axis by 90 degrees over z-axis
                Quaternion rotatedQuaternionYZ = rotationZ * rotatedQuaternionY;
                // Set the rotation of the transform to the new quaternion
                _transformMessage.transform.rotation.x = rotatedQuaternionYZ.x;
                _transformMessage.transform.rotation.y = rotatedQuaternionYZ.y;
                _transformMessage.transform.rotation.z = rotatedQuaternionYZ.z;
                _transformMessage.transform.rotation.w = rotatedQuaternionYZ.w;
            }




            ROSUnityManager.BroadcastTransform(_transformMessage);

        }

        protected override void ZOOnValidate()
        {
            base.ZOOnValidate();
            if (string.IsNullOrEmpty(ChildFrameID) == true)
            {
                ChildFrameID = Name;
            }
            if (string.IsNullOrEmpty(FrameID) == true)
            {
                if (transform.parent)
                {
                    FrameID = transform.parent.name;
                }
                else
                {
                    FrameID = ZOROSUnityManager.Instance.WorldFrameId;
                }
            }
            if (UpdateRateHz == 0)
            {
                UpdateRateHz = 10;
            }
            UpdateRateHz = 30; // force to 30hz
        }

        public override string Type
        {
            get { return "ros.publisher.transform"; }
        }


        public override void OnROSBridgeConnected(ZOROSUnityManager rosUnityManager)
        {
            Debug.Log("INFO: ZOROSTransformPublisher::OnROSBridgeConnected");

        }

        public override void OnROSBridgeDisconnected(ZOROSUnityManager rosUnityManager)
        {
            Debug.Log("INFO: ZOROSTransformPublisher::OnROSBridgeDisconnected");
        }


    }
}