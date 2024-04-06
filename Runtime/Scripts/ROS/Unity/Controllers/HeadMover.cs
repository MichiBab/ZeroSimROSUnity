using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ZO.ROS.MessageTypes;
using ZO.ROS.MessageTypes.Geometry;
using ZO.ROS.MessageTypes.Nav;
using ZO.ROS.MessageTypes.Std;
using ZO.ROS;
using ZO.ROS.Unity;
using ZO.Util;
using ZO.Physics;
using ZO.Document;
using System.Threading.Tasks;

public class HeadMover : ZOROSUnityGameObjectBase
{

    /* IDEA: When the Hinge joint call to a specific location is given,
    then the use limits will be disabled, the limits that result in the target location will be set, with min or max in the direction. If we want to go down for 1 degree, then we set  
    the min limit to our current minus one and set the current velocity to -50. If we get an up command, we set the max limit to current + the new wanted location and set the target velocity to +50. */

    private float min_head_deg = -90.0f;
    private float max_head_deg = 170.0f;
    private float min_yaw_deg = -120.0f;
    private float max_yaw_deg = 120.0f;

    private float target_head_deg = 0.0f;
    private float last_target_head_deg = 0.0f;

    private float target_yaw_deg = 0.0f;
    private float last_target_yaw_deg = 0.0f;

    private float min_target_vel = -150.0f;
    private float max_target_vel = 150.0f;



    public HingeJoint head;
    public HingeJoint yaw;



    public override string Type
    {
        get
        {
            return "controller.head_controller";
        }
    }

    void OnValidate()
    {
        // make sure we have a name
        if (string.IsNullOrEmpty(Name))
        {
            Name = gameObject.name + "_" + Type;
        }

    }

    protected override void ZOAwake()
    {
        base.ZOAwake();

        // make sure we have a name
        if (string.IsNullOrEmpty(Name))
        {
            Name = gameObject.name + "_" + Type;
        }

    }



    protected override void ZOFixedUpdate()
    {
        // update the motors from the target values
        if (last_target_head_deg != target_head_deg)
        {
            UpdateHeadMotors();
            last_target_head_deg = target_head_deg;
        }
        if (last_target_yaw_deg != target_yaw_deg)
        {
            UpdateYawMotors();
            last_target_yaw_deg = target_yaw_deg;
        }
    }

    protected override void ZOFixedUpdateHzSynchronized()
    {
        // publish odometry
        if (!ZOROSBridgeConnection.Instance.IsConnected)
        {
            return;
        }
    }

    private void UpdateYawMotors()
    {
        if (target_yaw_deg < last_target_yaw_deg)
        {
            JointLimits limit = yaw.limits;
            limit.min = target_yaw_deg;
            yaw.limits = limit;
            JointMotor motor = yaw.motor;
            motor.targetVelocity = min_target_vel;
            yaw.motor = motor;
        }
        else
        {
            JointLimits limit = yaw.limits;
            limit.max = target_yaw_deg;
            yaw.limits = limit;
            JointMotor motor = yaw.motor;
            motor.targetVelocity = max_target_vel;
            yaw.motor = motor;
        }
    }

    private void UpdateHeadMotors()
    {
        if (target_head_deg < last_target_head_deg)
        {
            JointLimits limit = head.limits;
            limit.min = target_head_deg;
            head.limits = limit;
            JointMotor motor = head.motor;
            motor.targetVelocity = min_target_vel;
            head.motor = motor;
        }
        else
        {
            JointLimits limit = head.limits;
            limit.max = target_head_deg;
            head.limits = limit;
            JointMotor motor = head.motor;
            motor.targetVelocity = max_target_vel;
            head.motor = motor;
        }
    }


    /// <summary>
    /// ROS control twist message topic.
    /// To test: `rosrun turtlebot3 turtlebot3_teleop_key`
    /// </summary>
    public string _ROSTopicSubscription = "/head/pitch_yaw_control";
    private Float32MultiArray _multiArrayMessage = new Float32MultiArray();


    public override void OnROSBridgeConnected(ZOROSUnityManager rosUnityManager)
    {
        Debug.Log("INFO: ZODifferentialDriveController::OnROSBridgeConnected");
        // subscribe to Twist Message
        ZOROSBridgeConnection.Instance.Subscribe<Float32MultiArray>(Name, _ROSTopicSubscription, _multiArrayMessage.MessageType, OnROSMessageReceived);
    }

    public override void OnROSBridgeDisconnected(ZOROSUnityManager rosUnityManager)
    {
        ZOROSBridgeConnection.Instance.UnAdvertise(_ROSTopicSubscription);
        Debug.Log("INFO: ZODifferentialDriveController::OnROSBridgeDisconnected");
    }



    public Task OnROSMessageReceived(ZOROSBridgeConnection rosBridgeConnection, ZOROSMessageInterface msg)
    {
        Float32MultiArray multiArray = (Float32MultiArray)msg;
        if (multiArray.data.Length == 2)
        {
            /* The conversion is as follows: head max = 3.14 val, head min = -2.11
                                             yaw max = 3.14 val, yaw min = -3.14.
                                             this means that 3.14 => 170 degrees, -2.11 => -90 degrees, 3.14 => 120 degrees, -3.14 => -120 degrees.
            */
            Debug.Log("HeadMover Received head and yaw control message: " + multiArray.data[0] + " " + multiArray.data[1]);
            target_head_deg = multiArray.data[0] * 54.14f;
            target_yaw_deg = multiArray.data[1] * 38.217f;

            return Task.CompletedTask;
            if (target_head_deg > max_head_deg)
            {
                target_head_deg = max_head_deg;
            }
            else if (target_head_deg < min_head_deg)
            {
                target_head_deg = min_head_deg;
            }

            if (target_yaw_deg > max_yaw_deg)
            {
                target_yaw_deg = max_yaw_deg;
            }
            else if (target_yaw_deg < min_yaw_deg)
            {
                target_yaw_deg = min_yaw_deg;

            }

        }
        return Task.CompletedTask;
    }


}


